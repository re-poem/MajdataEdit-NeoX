using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Models.Setting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MajdataEdit_NeoX.Models.Connection;

internal sealed class PlayerConnection : IAsyncDisposable
{
    private sealed class Endpoint : IAsyncDisposable
    {
        public Endpoint(string ip)
        {
            Ip = ip;
            Outgoing = Channel.CreateUnbounded<RequestEnvelope>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public string Ip { get; }
        public ClientWebSocket? Client;
        public readonly Channel<RequestEnvelope> Outgoing;
        public readonly SemaphoreSlim ConnectGate = new(1, 1);
        public readonly CancellationTokenSource LifetimeCts = new();
        public readonly Lock StateLock = new();
        public ViewSummary Summary = new();
        public Task? SendLoop;
        public Task? ReceiveLoop;
        public bool Disposed;

        public bool IsConnected => Client?.State == WebSocketState.Open;

        public async Task<bool> ConnectAsync(PlayerConnection owner)
        {
            if (IsConnected) return true;

            await ConnectGate.WaitAsync(LifetimeCts.Token);
            try
            {
                if (IsConnected) return true;

                var client = new ClientWebSocket();
                client.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                await client.ConnectAsync(new Uri(MajWsProtocol.CreateServerUrl(Ip)), LifetimeCts.Token);

                var previous = Interlocked.Exchange(ref Client, client);
                if (previous is not null)
                {
                    try { await previous.CloseAsync(WebSocketCloseStatus.NormalClosure, "rotate", CancellationToken.None); }
                    catch { /* swallow */ }
                    previous.Dispose();
                }

                SendLoop = Task.Run(() => owner.SendLoopAsync(this, LifetimeCts.Token));
                ReceiveLoop = Task.Run(() => owner.ReceiveLoopAsync(this, LifetimeCts.Token));
                owner.OnConnectedChanged?.Invoke(true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ConnectGate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Disposed) return;
            Disposed = true;
            LifetimeCts.Cancel();
            Outgoing.Writer.TryComplete();

            var client = Interlocked.Exchange(ref Client, null);
            if (client is not null)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None);
                }
                catch { /* swallow */ }
                client.Dispose();
            }

            if (SendLoop is not null)
            {
                try { await SendLoop; }
                catch (OperationCanceledException) { }
                catch { /* swallow */ }
            }
            if (ReceiveLoop is not null)
            {
                try { await ReceiveLoop; }
                catch (OperationCanceledException) { }
                catch { /* swallow */ }
            }

            LifetimeCts.Dispose();
            ConnectGate.Dispose();
        }
    }

    private readonly Lock _endpointsLock = new();
    private readonly List<Endpoint> _endpoints = new();
    private int _activeConnectionCount;
    private bool _disposed;

    public PlayerConnection(IEnumerable<string> ips)
    {
        SetEndpoints(ips);
    }

    public bool IsConnected => ActiveConnectionCount > 0;

    public int ActiveConnectionCount
    {
        get
        {
            lock (_endpointsLock) return _activeConnectionCount;
        }
    }

    public ViewSummary ViewSummary
    {
        get
        {
            var endpoint = PrimaryEndpoint;
            if (endpoint is null) return new();
            lock (endpoint.StateLock) return endpoint.Summary;
        }
    }

    public ViewStatus State => ViewSummary.State;

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

    public async Task<bool> ConnectAsync()
    {
        if (_disposed) return false;
        var endpoints = SnapshotEndpoints();
        if (endpoints.Count == 0) return false;

        var results = await Task.WhenAll(endpoints.Select(endpoint => endpoint.ConnectAsync(this)));
        UpdateActiveConnectionCount();
        return results.Any(result => result);
    }

    public async Task SetPlayersAsync(IEnumerable<string> ips)
    {
        if (_disposed) return;

        var normalized = NormalizeIps(ips);
        var oldEndpoints = SnapshotEndpoints();
        lock (_endpointsLock)
        {
            _endpoints.Clear();
            _endpoints.AddRange(normalized.Select(ip => new Endpoint(ip)));
            _activeConnectionCount = 0;
        }

        await Task.WhenAll(oldEndpoints.Select(endpoint => endpoint.DisposeAsync().AsTask()));
        UpdateActiveConnectionCount();
        if (!_disposed) await ConnectAsync();
    }

    public Task LoadAsync(string trackPath, string imagePath, string? videoPath)
        => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Load, new LoadRequest
        {
            TrackPath = trackPath,
            ImagePath = imagePath,
            VideoPath = videoPath,
        }));

    public Task SettingAsync(MajViewSetting viewSetting, MajVolumeSetting volumeSetting)
    {
        var payload = new JObject
        {
            ["viewSetting"] = JToken.FromObject(viewSetting,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
            ["volumeSetting"] = JToken.FromObject(volumeSetting,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
        };
        return BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Setting, payload));
    }

    public Task ParseAsync(string simaiFumen, int clockCount, float offset, string title,
        string artist, string level, string designer, int difficulty)
        => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Parse, new ParseRequest
        {
            SimaiFumen = simaiFumen,
            ClockCount = clockCount,
            Offset = offset,
            Title = title,
            Artist = artist,
            Level = level,
            Designer = designer,
            Difficulty = difficulty,
        }));

    public Task PlayAsync(PlaybackMode mode, double startAt, float speed, string? maidataPath)
        => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Play, new PlayRequest
        {
            PlayMode = mode,
            StartAt = startAt,
            Speed = speed,
            MaidataPath = maidataPath,
        }));

    public Task PauseAsync() => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Pause, null));
    public Task ResumeAsync() => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Resume, null));
    public Task StopAsync() => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Stop, null));
    public Task ResetAsync() => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.Reset, null));
    public Task StateAsync() => BroadcastAsync(endpoint => EnqueueAsync(endpoint, RequestType.State, null));

    private static string[] NormalizeIps(IEnumerable<string> ips)
        => ips
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Select(ip => ip.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private void SetEndpoints(IEnumerable<string> ips)
    {
        lock (_endpointsLock)
            _endpoints.AddRange(NormalizeIps(ips).Select(ip => new Endpoint(ip)));
    }

    private List<Endpoint> SnapshotEndpoints()
    {
        lock (_endpointsLock) return _endpoints.ToList();
    }

    private Endpoint? PrimaryEndpoint
    {
        get
        {
            lock (_endpointsLock)
                return _endpoints.FirstOrDefault(endpoint => endpoint.IsConnected);
        }
    }

    private async Task BroadcastAsync(Func<Endpoint, Task> operation)
    {
        var endpoints = SnapshotEndpoints().Where(endpoint => endpoint.IsConnected).ToArray();
        await Task.WhenAll(endpoints.Select(async endpoint =>
        {
            try { await operation(endpoint); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerConnection] broadcast {endpoint.Ip}: {ex.Message}");
            }
        }));
    }

    private async Task EnqueueAsync(Endpoint endpoint, RequestType type, object? payload)
    {
        if (endpoint.LifetimeCts.IsCancellationRequested)
            throw new ObjectDisposedException(nameof(PlayerConnection));

        JToken? requestData = payload switch
        {
            null => null,
            JToken jt => jt,
            _ => JToken.FromObject(payload,
                JsonSerializer.Create(MajProtocolSerializer.DefaultSettings)),
        };

        await endpoint.Outgoing.Writer.WriteAsync(new RequestEnvelope
        {
            ProtocolVersion = ProtocolVersion.Current,
            RequestType = type,
            RequestData = requestData,
        }, endpoint.LifetimeCts.Token);
    }

    private async Task SendLoopAsync(Endpoint endpoint, CancellationToken ct)
    {
        var client = endpoint.Client;
        if (client is null) return;
        try
        {
            await foreach (var envelope in endpoint.Outgoing.Reader.ReadAllAsync(ct))
            {
                var bytes = Encoding.UTF8.GetBytes(MajProtocolSerializer.Serialize(envelope));
                await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                    endOfMessage: true, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] send {endpoint.Ip}: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(Endpoint endpoint, CancellationToken ct)
    {
        var client = endpoint.Client;
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
                    result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                ProcessIncoming(endpoint, Encoding.UTF8.GetString(ms.ToArray()));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] receive {endpoint.Ip}: {ex.Message}");
        }
        finally
        {
            UpdateActiveConnectionCount();
        }
    }

    private void ProcessIncoming(Endpoint endpoint, string json)
    {
        try
        {
            var envelope = MajProtocolSerializer.Deserialize<ResponseEnvelope>(json);
            if (envelope is null) return;
            var env = envelope.Value;

            if (env.ResponseType == ResponseType.Event)
            {
                var evt = MajProtocolSerializer.DeserializeResponsePayload<EventResponse>(env);
                if (evt is not null) DispatchEvent(endpoint, evt.Value);
                return;
            }

            if (env.ResponseType == ResponseType.Error)
            {
                if (env.ResponseData is JObject jobj)
                    OnViewError?.Invoke(jobj["errMsg"]?.Value<string>());
                return;
            }

            if (env.ResponseData is not null)
            {
                var summary = MajProtocolSerializer.DeserializeResponsePayload<ViewSummary>(env);
                if (summary is not null) UpdateSummary(endpoint, summary.Value);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerConnection] parse {endpoint.Ip}: {ex.Message}");
        }
    }

    private void DispatchEvent(Endpoint endpoint, EventResponse evt)
    {
        if (evt.Summary is not null) UpdateSummary(endpoint, evt.Summary.Value);

        switch (evt.EventType)
        {
            case EventType.LoadOk: OnLoadFinished?.Invoke(); break;
            case EventType.PlayStarted: OnPlayStarted?.Invoke(); break;
            case EventType.PlayResumed: OnPlayResumed?.Invoke(); break;
            case EventType.PlayPaused: OnPlayPaused?.Invoke(); break;
            case EventType.PlayStopped: OnPlayStopped?.Invoke(); break;
        }
    }

    private void UpdateSummary(Endpoint endpoint, ViewSummary newSummary)
    {
        ViewStatus oldState;
        lock (endpoint.StateLock)
        {
            oldState = endpoint.Summary.State;
            endpoint.Summary = newSummary;
        }

        if (ReferenceEquals(PrimaryEndpoint, endpoint) && oldState != newSummary.State)
            OnViewStateChanged?.Invoke(oldState, newSummary.State);
        if (newSummary.State == ViewStatus.Error)
            OnViewError?.Invoke(newSummary.ErrMsg);
    }

    private void UpdateActiveConnectionCount()
    {
        int count;
        lock (_endpointsLock)
        {
            count = _endpoints.Count(endpoint => endpoint.IsConnected);
            if (count == _activeConnectionCount) return;
            _activeConnectionCount = count;
        }
        OnConnectedChanged?.Invoke(count > 0);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        var endpoints = SnapshotEndpoints();
        lock (_endpointsLock)
        {
            _endpoints.Clear();
            _activeConnectionCount = 0;
        }
        await Task.WhenAll(endpoints.Select(endpoint => endpoint.DisposeAsync().AsTask()));
        OnConnectedChanged?.Invoke(false);
    }
}
