using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Models.Setting;
using MajProtocol.Generated;
using MajProtocol.Standard;
using MajProtocol.Standard.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebSocketSharp;

namespace MajdataEdit_NeoX.Models.Connection;

/// <summary>
/// 编辑器侧的 MajProtocol 客户端，把播放控制指令发给播放器（MajdataViewX）。
///
/// 与 MajProtocol 示例实现的差别：示例的 <c>MajWebSocketClient</c> 是静态单例、单连接
/// （协议本身也假设一条连接）；编辑器需要同时驱动多个播放器（<see cref="MajEditSetting.PlayerIps"/>），
/// 因此这里为每个 IP 建一个 <see cref="Endpoint"/>，各自持有 WebSocket 与发送队列，
/// 协议层仍然只用 <c>MajProtocol.Standard</c> 的 packet 与 generated 路由接口。
///
/// 线上是二进制帧：每个包第一个字节是 <see cref="OpCode"/>，其后按 packet 结构体字段顺序排列。
/// </summary>
internal sealed class PlayerConnection : IAsyncDisposable
{
    /// <summary>
    /// 一个播放器连接。实现 <see cref="IMajProtocolClient"/> 来复用协议生成的按 OpCode 路由，
    /// 自己只管连接生命周期与请求排队。
    /// </summary>
    private sealed class Endpoint : IMajProtocolClient, IAsyncDisposable
    {
        /// <summary>
        /// 一个请求最多等多久响应。播放器加载资源/解析谱面可能比较慢，给足余量；
        /// 超时后放行下一条，避免一条没回应的请求把队列堵死。
        /// </summary>
        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(15);

        private readonly PlayerConnection _owner;
        private readonly Channel<byte[]> _outgoing;
        private readonly SemaphoreSlim _connectGate = new(1, 1);
        private readonly SemaphoreSlim _requestGate = new(1, 1);
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Lock _pendingLock = new();

        private WebSocket? _socket;
        private Task? _sendLoop;
        private bool _disposed;

        // 一次只有一个请求在途：_pending 是它的等待槽，_pendingResponseType 用来认领响应。
        // 串行化是协议自带的约束（响应里没有请求 id，类型也唯一的只有同一时刻的一条请求）。
        private TaskCompletionSource<bool>? _pending;
        private Type? _pendingResponseType;

        public Endpoint(PlayerConnection owner, string ip)
        {
            _owner = owner;
            Ip = ip;
            _outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public string Ip { get; }

        public bool IsConnected => _socket?.ReadyState == WebSocketState.Open;

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected) return true;

            await _connectGate.WaitAsync(_lifetime.Token);
            try
            {
                if (IsConnected) return true;

                var socket = new WebSocket(MajWsProtocol.CreateServerUrl(Ip));
                socket.OnMessage += OnMessage;
                socket.OnClose += OnClose;

                // WebSocketSharp 的 Connect 是同步阻塞的，挪到线程池避免卡住 UI 线程。
                await Task.Run(socket.Connect, _lifetime.Token);
                if (socket.ReadyState != WebSocketState.Open)
                {
                    socket.Close();
                    return false;
                }

                var previous = Interlocked.Exchange(ref _socket, socket);
                previous?.Close();

                // 协议要求连接建立后立刻握手。示例服务端在版本一致时不回包，
                // 因此这里不等待 HandshakeResponse：只有收到 accepted=false 才断开。
                Enqueue(new HandshakeRequest { protocolVersion = Properties.ProtocolVersion }.ToBytes());

                // 发送循环每次取 _socket 的当前值，可以跨重连复用；
                // 若重连时再起一个，两个 reader 会竞争同一个 SingleReader channel，
                // 包顺序就乱了。所以只在第一次连接时启动。
                _sendLoop ??= Task.Run(() => SendLoopAsync(_lifetime.Token));
                _owner.NotifyConnectionCountChanged();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerConnection] connect {Ip}: {ex.Message}");
                return false;
            }
            finally
            {
                _connectGate.Release();
            }
        }

        /// <summary>把一个已序列化的 packet 排进发送队列。</summary>
        public void Enqueue(byte[] packet) => _outgoing.Writer.TryWrite(packet);

        /// <summary>
        /// 发一个请求并等它对应的响应，返回播放器是否 accepted。
        /// 请求在 endpoint 内部串行：后来者等前一条响应回来才发出去，调用方因此不需要自己等空闲。
        /// 断开、超时或取消都按未接受处理。
        /// </summary>
        public async Task<bool> RequestAsync<TResponse>(byte[] packet)
        {
            if (!IsConnected) return false;

            try
            {
                await _requestGate.WaitAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            try
            {
                if (!IsConnected) return false;

                var pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_pendingLock)
                {
                    _pending = pending;
                    _pendingResponseType = typeof(TResponse);
                }

                Enqueue(packet);
                return await pending.Task.WaitAsync(ResponseTimeout, _lifetime.Token);
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerConnection] {typeof(TResponse).Name} timeout {Ip}");
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerConnection] {typeof(TResponse).Name} {Ip}: {ex.Message}");
                return false;
            }
            finally
            {
                lock (_pendingLock)
                {
                    _pending = null;
                    _pendingResponseType = null;
                }
                _requestGate.Release();
            }
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var packet in _outgoing.Reader.ReadAllAsync(ct))
                {
                    var socket = _socket;
                    if (socket is null || socket.ReadyState != WebSocketState.Open) continue;
                    socket.Send(packet);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerConnection] send {Ip}: {ex.Message}");
            }
        }

        private void OnMessage(object? sender, MessageEventArgs e)
        {
            // RawData 是完整的二进制帧；按 OpCode 分派给下面各个 On*ResponseReceived。
            ((IMajProtocolClient)this).Handle(e.RawData);
        }

        /// <summary>结算在途请求；响应对不上当前请求（例如迟到的握手回包）时忽略。</summary>
        private void CompleteResponse<TResponse>(bool accepted)
        {
            lock (_pendingLock)
            {
                if (_pendingResponseType != typeof(TResponse)) return;
                _pending?.TrySetResult(accepted);
            }
        }

        private void OnClose(object? sender, CloseEventArgs e)
        {
            // 连接断了就没人会再回响应：放掉在途请求，别让命令一直等到超时。
            lock (_pendingLock) _pending?.TrySetResult(false);
            _owner.NotifyConnectionCountChanged();
        }

        public void OnHandshakeResponseReceived(HandshakeResponse req)
        {
            // 版本不匹配：服务端会主动断开，这里同步收尾。
            if (!req.accepted) Close();
        }

        public void OnFilesResponseReceived(FilesResponse req) => CompleteResponse<FilesResponse>(req.accepted);

        public void OnSimaiResponseReceived(SimaiResponse req) => CompleteResponse<SimaiResponse>(req.accepted);

        public void OnPlayResponseReceived(PlayResponse req) => CompleteResponse<PlayResponse>(req.accepted);

        public void OnPauseResponseReceived(PauseResponse req) => CompleteResponse<PauseResponse>(req.accepted);

        public void OnOptionsResponseReceived(OptionsResponse req) => CompleteResponse<OptionsResponse>(req.accepted);

        public void OnUnloadResponseReceived(UnloadResponse req) => CompleteResponse<UnloadResponse>(req.accepted);

        // 目前编辑器不主动查询状态（状态只由自己的命令驱动），有等待者时仍按 accepted 语义结算。
        public void OnStateResponseReceived(StateResponse req) => CompleteResponse<StateResponse>(req.isPlaying);

        private void Close() => _socket?.Close();

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _lifetime.Cancel();
            _outgoing.Writer.TryComplete();
            lock (_pendingLock) _pending?.TrySetResult(false);

            var socket = Interlocked.Exchange(ref _socket, null);
            if (socket is not null)
            {
                try { socket.Close(); }
                catch { /* 关不掉不影响释放 */ }
            }

            if (_sendLoop is not null)
            {
                try { await _sendLoop; }
                catch (OperationCanceledException) { }
                catch { /* 发送循环自身的异常已在内部记录 */ }
            }

            _lifetime.Dispose();
            _connectGate.Dispose();
            _requestGate.Dispose();
        }
    }

    private readonly Lock _endpointsLock = new();
    private readonly List<Endpoint> _endpoints = new();
    private int _activeConnectionCount;
    private bool _disposed;

    public PlayerConnection(IEnumerable<string> ips)
    {
        lock (_endpointsLock)
            _endpoints.AddRange(NormalizeIps(ips).Select(ip => new Endpoint(this, ip)));
    }

    public bool IsConnected => ActiveConnectionCount > 0;

    public int ActiveConnectionCount
    {
        get
        {
            lock (_endpointsLock) return _activeConnectionCount;
        }
    }

    /// <summary>连接状态变化：有播放器连上 true / 全部断开 false。</summary>
    public event Action<bool>? OnConnectedChanged;

    public async Task<bool> ConnectAsync()
    {
        if (_disposed) return false;
        var endpoints = SnapshotEndpoints();
        if (endpoints.Count == 0) return false;

        var results = await Task.WhenAll(endpoints.Select(endpoint => endpoint.ConnectAsync()));
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
            _endpoints.AddRange(normalized.Select(ip => new Endpoint(this, ip)));
            _activeConnectionCount = 0;
        }

        await Task.WhenAll(oldEndpoints.Select(endpoint => endpoint.DisposeAsync().AsTask()));
        UpdateActiveConnectionCount();
        if (!_disposed) await ConnectAsync();
    }

    /// <summary>
    /// 让播放器装载曲目资源。三个参数都是播放器侧可达的路径；
    /// 拿不到本地路径（Android 的 <c>content://</c>、Browser 沙箱）时传 <see cref="string.Empty"/>，
    /// 这是协议规定的「无法提供该字段」的表达方式。
    /// </summary>
    public Task<bool> FilesAsync(string trackPath, string jacketPath, string moviePath)
        => BroadcastAsync<FilesResponse>(new FilesRequest
        {
            trackPath = trackPath,
            jacketPath = jacketPath,
            moviePath = moviePath,
        }.ToBytes());

    /// <summary>
    /// 推送视图与音量设置。协议只给常用项留了独立字段，其余 ViewSetting 项
    /// 打包成 JSON 放进 <c>extra</c>。
    /// </summary>
    public Task<bool> OptionsAsync(MajViewSetting viewSetting, MajVolumeSetting volumeSetting)
        => BroadcastAsync<OptionsResponse>(new OptionsRequest
        {
            tapSpeed = viewSetting.TapSpeed,
            touchSpeed = viewSetting.TouchSpeed,

            trackVolume = volumeSetting.Track,
            answerVolume = volumeSetting.Answer,
            tapVolume = volumeSetting.Tap,
            slideVolume = volumeSetting.Slide,
            breakVolume = volumeSetting.Break,
            breakSlideVolume = volumeSetting.BreakSlide,
            exVolume = volumeSetting.Ex,
            touchVolume = volumeSetting.Touch,
            hanabiVolume = volumeSetting.Hanabi,

            extra = BuildExtraOptionsJson(viewSetting),
        }.ToBytes());

    public Task<bool> SimaiAsync(string content, float offset, string title, string artist,
        string designer, string difficultyValue, int difficultyIndex)
        => BroadcastAsync<SimaiResponse>(new SimaiRequest
        {
            offset = offset,
            title = title,
            artist = artist,
            designer = designer,
            difficultyValue = difficultyValue,
            difficultyIndex = difficultyIndex,
            content = content,
        }.ToBytes());

    /// <summary>
    /// 开始播放。协议用 <c>showIntroAnimation</c> / <c>record</c> 两个布尔量表达播放模式，
    /// 取代了旧版的 <c>PlaybackMode</c> 枚举；录制路径由播放器自己决定。
    /// </summary>
    /// <param name="speed">播放倍速，编辑器播放头与播放器按同一倍速推算。</param>
    public Task<bool> PlayAsync(PlaybackMode mode, double position, float speed)
        => BroadcastAsync<PlayResponse>(new PlayRequest
        {
            position = position,
            speed = speed,
            showIntroAnimation = mode is PlaybackMode.IncludeOp or PlaybackMode.Record,
            record = mode is PlaybackMode.Record,
        }.ToBytes());

    public Task<bool> PauseAsync() => BroadcastAsync<PauseResponse>(new PauseRequest().ToBytes());

    public Task<bool> UnloadAsync() => BroadcastAsync<UnloadResponse>(new UnloadRequest().ToBytes());

    public Task<bool> StateAsync() => BroadcastAsync<StateResponse>(new StateRequest().ToBytes());

    private static string[] NormalizeIps(IEnumerable<string> ips)
        => ips
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Select(ip => ip.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private List<Endpoint> SnapshotEndpoints()
    {
        lock (_endpointsLock) return _endpoints.ToList();
    }

    /// <summary>
    /// 把 ViewSetting 里没有独立协议字段的项打包成 JSON 塞进 <c>extra</c>。
    /// 键名与设置文件里的 <c>[JsonPropertyName]</c> 一致（camelCase）。
    /// </summary>
    private static string BuildExtraOptionsJson(MajViewSetting view) => new JsonObject
    {
        ["smoothSlideAnime"] = view.SmoothSlideAnime,
        ["backgroundDim"] = view.BackgroundDim,
        ["backgroundOutsideDim"] = view.BackgroundOutsideDim,
        ["comboStatusType"] = (int)view.ComboStatusType,
        ["autoMode"] = (int)view.AutoMode,
        ["showHand"] = view.ShowHand,
        ["uiType"] = (int)view.UIType,
        ["globalAudioOffset"] = view.GlobalAudioOffset,
        ["legacySlideLayer"] = view.LegacySlideLayer,
        ["mineAutoSlide"] = view.MineAutoSlide,
        ["outputFps"] = view.OutputFps,
        ["exportQuality"] = (int)view.ExportQuality,
        ["resizeBg"] = view.ResizeBg,
    }.ToJsonString();

    /// <summary>
    /// 把一条请求发给所有已连接播放器并等各自的响应。任一播放器接受即算成功，
    /// 没有播放器在线时按失败处理。
    /// </summary>
    private async Task<bool> BroadcastAsync<TResponse>(byte[] payload)
    {
        var endpoints = SnapshotEndpoints().Where(endpoint => endpoint.IsConnected).ToArray();
        if (endpoints.Length == 0) return false;

        var results = await Task.WhenAll(endpoints.Select(endpoint => endpoint.RequestAsync<TResponse>(payload)));
        return results.Any(accepted => accepted);
    }

    private void NotifyConnectionCountChanged() => UpdateActiveConnectionCount();

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
