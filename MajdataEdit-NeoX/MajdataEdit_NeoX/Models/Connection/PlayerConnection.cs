using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MajProtocol;
using MajProtocol.Serialization;
using MajProtocol.Types;
using MajProtocol.Types.Enums;
using MajProtocol.Types.Request;
using MajProtocol.Types.Response;
using MajdataEdit_NeoX.Models.Setting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MajdataEdit_NeoX.Base;

namespace MajdataEdit_NeoX.Models.Connection;

internal sealed class PlayerConnection : IAsyncDisposable
{
    public bool IsConnected => _client?.State == WebSocketState.Open;

    public ViewSummary ViewSummary
    {
        get { lock (_stateLock) return _summary; }
    }

    public ViewStatus State
    {
        get { lock (_stateLock) return _summary.State; }
    }

    /// <summary>所有状态变化（含 Idle↔Loaded、Loaded↔Playing、Playing↔Paused 等）会触发。</summary>
    public event Action<ViewStatus, ViewStatus>? OnViewStateChanged;

    /// <summary>服务端响应 Load 后推送的 LoadOk 事件。</summary>
    public event Action? OnLoadFinished;

    /// <summary>服务端响应 PlayAsync（首启）后推送的 PlayStarted 事件。</summary>
    public event Action? OnPlayStarted;

    /// <summary>服务端响应 Resume 或 PlayAsync（从 Paused）后推送的 PlayResumed 事件。</summary>
    public event Action? OnPlayResumed;

    /// <summary>服务端响应 Pause 后推送的 PlayPaused 事件。</summary>
    public event Action? OnPlayPaused;

    /// <summary>服务端响应 Stop 或 Reset 后推送的 PlayStopped 事件。</summary>
    public event Action? OnPlayStopped;

    /// <summary>ViewSummary.State 进入 Error 时触发。</summary>
    public event Action<string?>? OnViewError;

    /// <summary>连接状态变化：连上 true / 断开 false（对端关闭 / 接收循环退出）。</summary>
    public event Action<bool>? OnConnectedChanged;

    private ViewSummary _summary = new();
    private readonly Lock _stateLock = new();

    private ClientWebSocket? _client;
    private readonly Channel<RequestEnvelope> _outgoing =
        Channel.CreateUnbounded<RequestEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Task? _sendLoop;
    private Task? _recvLoop;
    private bool _disposed;

    public async Task<bool> ConnectAsync(string? url = null)
    {
        if (IsConnected) return true;
        url ??= MajWsProtocol.ServerUrl;
        return await ConnectToPlayerAsync(url);
    }

    public Task LoadAsync(string trackPath, string imagePath, string? videoPath)
        => EnqueueAsync(RequestType.Load, new LoadRequest
        {
            TrackPath = trackPath,
            ImagePath = imagePath,
            VideoPath = videoPath,
        });

    public Task SettingAsync(MajViewSetting viewSetting, MajVolumeSetting volumeSetting)
    {
        var payload = new JObject
        {
            ["viewSetting"] = JToken.FromObject(viewSetting,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
            ["volumeSetting"] = JToken.FromObject(volumeSetting,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
        };
        return EnqueueAsync(RequestType.Setting, payload);
    }

    public Task ParseAsync(
        string simaiFumen,
        int clockCount,
        float offset,
        string title,
        string artist,
        string level,
        string designer,
        int difficulty)
        => EnqueueAsync(RequestType.Parse, new ParseRequest
        {
            SimaiFumen = simaiFumen,
            ClockCount = clockCount,
            Offset = offset,
            Title = title,
            Artist = artist,
            Level = level,
            Designer = designer,
            Difficulty = difficulty,
        });

    public Task PlayAsync(PlaybackMode mode, double startAt, float speed, string? maidataPath)
        => EnqueueAsync(RequestType.Play, new PlayRequest
        {
            PlayMode = mode,
            StartAt = startAt,
            Speed = speed,
            MaidataPath = maidataPath,
        });

    public Task PauseAsync() => EnqueueAsync(RequestType.Pause, null);
    public Task ResumeAsync() => EnqueueAsync(RequestType.Resume, null);
    public Task StopAsync() => EnqueueAsync(RequestType.Stop, null);
    public Task ResetAsync() => EnqueueAsync(RequestType.Reset, null);
    public Task StateAsync() => EnqueueAsync(RequestType.State, null);

    //==================== internal: connection / loops ====================

    private async Task<bool> ConnectToPlayerAsync(string url)
    {
        await _connectGate.WaitAsync(_lifetimeCts.Token);
        try
        {
            if (IsConnected) return true;

            var client = new ClientWebSocket();
            client.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            await client.ConnectAsync(new Uri(url), _lifetimeCts.Token);

            // 替换旧 client；起循环
            var previous = Interlocked.Exchange(ref _client, client);
            if (previous is not null)
            {
                try { await previous.CloseAsync(WebSocketCloseStatus.NormalClosure, "rotate", CancellationToken.None); }
                catch { /* swallow */ }
            }

            _sendLoop = Task.Run(() => SendLoopAsync(_lifetimeCts.Token));
            _recvLoop = Task.Run(() => ReceiveLoopAsync(_lifetimeCts.Token));
            OnConnectedChanged?.Invoke(true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private async Task EnqueueAsync(RequestType type, object? payload)
    {
        if (_lifetimeCts.IsCancellationRequested)
            throw new ObjectDisposedException(nameof(PlayerConnection));

        JToken? requestData = payload switch
        {
            null => null,
            JToken jt => jt,
            _ => JToken.FromObject(payload,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
        };

        var envelope = new RequestEnvelope
        {
            ProtocolVersion = ProtocolVersion.Current,
            RequestType = type,
            RequestData = requestData,
        };
        await _outgoing.Writer.WriteAsync(envelope, _lifetimeCts.Token);
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        var client = _client;
        if (client is null) return;
        try
        {
            await foreach (var envelope in _outgoing.Reader.ReadAllAsync(ct))
            {
                var json = MajProtocolSerializer.Serialize(envelope);
                var bytes = Encoding.UTF8.GetBytes(json);
                var seg = new ArraySegment<byte>(bytes);
                await client.SendAsync(seg, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException) { /* end of life */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] send loop: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var client = _client;
        if (client is null) return;
        var buffer = new byte[16 * 1024];
        try
        {
            while (!ct.IsCancellationRequested && client.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    var seg = new ArraySegment<byte>(buffer);
                    result = await client.ReceiveAsync(seg, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(ms.ToArray());
                ProcessIncoming(json);
            }
        }
        catch (OperationCanceledException) { /* end of life */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] recv loop: {ex.Message}");
        }
        finally
        {
            OnConnectedChanged?.Invoke(false);
        }
    }

    private void ProcessIncoming(string json)
    {
        try
        {
            var envelope = MajProtocolSerializer.Deserialize<ResponseEnvelope>(json);
            if (envelope is null) return;
            var env = envelope.Value;

            // 事件通道：服务端主动推送的语义事件
            if (env.ResponseType == ResponseType.Event)
            {
                var evt = MajProtocolSerializer.DeserializeResponsePayload<EventResponse>(env);
                if (evt is not null) DispatchEvent(evt.Value);
                return;
            }

            if (env.ResponseType == ResponseType.Error)
            {
                // 协议规定错误信息走 ViewSummary.ErrMsg；ResponseEnvelope 没有 Error 字段。
                if (env.ResponseData is JObject jobj)
                {
                    var errMsg = jobj["errMsg"]?.Value<string>();
                    OnViewError?.Invoke(errMsg);
                }
                return;
            }

            // Ok 响应（State 查询 / Setting / Reset 等）：可能带 ViewSummary，需要更新本地镜像
            if (env.ResponseData is null) return;
            var summary = MajProtocolSerializer.DeserializeResponsePayload<ViewSummary>(env);
            if (summary is not null) UpdateSummary(summary.Value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] parse incoming: {ex.Message}");
        }
    }

    private void DispatchEvent(EventResponse evt)
    {
        // EventResponse 自带的 Summary 先更新本地镜像（含 timeline 与 error 信息）
        if (evt.Summary is not null) UpdateSummary(evt.Summary.Value);

        switch (evt.EventType)
        {
            case EventType.LoadOk:
                OnLoadFinished?.Invoke();
                break;
            case EventType.PlayStarted:
                OnPlayStarted?.Invoke();
                break;
            case EventType.PlayResumed:
                OnPlayResumed?.Invoke();
                break;
            case EventType.PlayPaused:
                OnPlayPaused?.Invoke();
                break;
            case EventType.PlayStopped:
                OnPlayStopped?.Invoke();
                break;
        }
    }

    private void UpdateSummary(ViewSummary newSummary)
    {
        ViewStatus oldState;
        lock (_stateLock)
        {
            oldState = _summary.State;
            _summary = newSummary;
        }

        // timeline 等字段的高频刷新不广播；只有真正的状态迁移才触发。
        if (oldState != newSummary.State)
            OnViewStateChanged?.Invoke(oldState, newSummary.State);

        // 大多数 Event 让OnViewStateChanged干就行；这里只关心 Error 状态。
        if (newSummary.State == ViewStatus.Error)
            OnViewError?.Invoke(newSummary.ErrMsg);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _lifetimeCts.Cancel();
        _outgoing.Writer.TryComplete();

        var client = Interlocked.Exchange(ref _client, null);
        if (client is not null)
        {
            try
            {
                if (client.State == WebSocketState.Open)
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown",
                        CancellationToken.None);
            }
            catch { /* swallow */ }
            client.Dispose();
        }

        if (_sendLoop is not null)
        {
            try { await _sendLoop; }
            catch (OperationCanceledException) { }
            catch { /* swallow */ }
        }
        if (_recvLoop is not null)
        {
            try { await _recvLoop; }
            catch (OperationCanceledException) { }
            catch { /* swallow */ }
        }

        _lifetimeCts.Dispose();
        _connectGate.Dispose();
    }
}