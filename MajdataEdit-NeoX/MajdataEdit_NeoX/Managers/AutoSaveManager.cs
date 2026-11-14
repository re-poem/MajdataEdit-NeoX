using Avalonia.Threading;
using MajdataEdit_NeoX.Models.IO;
using MajdataEdit_NeoX.Models.Setting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Managers;

/// <summary>
/// 自动保存调度：编辑防抖 + 陈旧上界 + 关键节点强制 flush。
///
/// 取代 Neo 那份「静态单例 + 60 秒轮询 + IsFileChanged 布尔」：
/// <list type="bullet">
///   <item>没有任何静态状态，谁持有谁用，可测；</item>
///   <item>不是定时轮询，而是「停止输入 N 秒后写」+ 节点强制写，最近的改动先保住；</item>
///   <item>不持有 MainViewModel，只吃三个委托：当前谱面、当前内容、当前设置。</item>
/// </list>
///
/// 只有防抖是不够的：连续输入时每一笔都会把计时器重置，只要两次击键间隔短于防抖秒数就永远不会写，
/// 写完一份也不再有计时器。所以另加一条<b>陈旧上界</b>——从本轮第一笔改动起算，最多
/// <see cref="MaxStale"/> 就必须落一份快照，不受后续输入影响；再配一次失败重试，
/// 进程被强杀时最多丢掉上界那几秒，而不是整段。
///
/// 防抖计时器是 <see cref="DispatcherTimer"/>，因此 <c>_content()</c> 的 deparse 都在 UI 线程上跑；
/// 落盘的 SQLite 调用由 <see cref="AutoSaveStore"/> 自己甩到后台。
/// </summary>
public sealed class AutoSaveManager : IDisposable
{
    /// <summary>两次写入之间的硬间隔，防止重试把磁盘写爆。</summary>
    private static readonly TimeSpan MinWriteInterval = TimeSpan.FromSeconds(1);

    /// <summary>陈旧上界：本轮第一笔改动之后最多撑这么久，连续输入也照样落盘。</summary>
    private static readonly TimeSpan MaxStale = TimeSpan.FromSeconds(5);

    /// <summary>没写成（门控拦下 / 落盘失败）之后的重试间隔。</summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly AutoSaveStore _store;
    private readonly Func<ChartFolder?> _folder;
    private readonly Func<string?> _content;
    private readonly Func<MajEditSetting> _settings;

    private readonly object _gate = new();
    /// <summary>写入串行化：<c>Wait(0)</c> 拿不到就是正在写，强制 flush 则排队等。</summary>
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private IDisposable? _debounce;
    private IDisposable? _stale;
    private IDisposable? _retry;
    private bool _dirty;
    private int _suppressed;
    private long _lastWriteTicks;
    private bool _disposed;

    public AutoSaveManager(
        AutoSaveStore store,
        Func<ChartFolder?> folder,
        Func<string?> content,
        Func<MajEditSetting> settings)
    {
        _store = store;
        _folder = folder;
        _content = content;
        _settings = settings;
    }

    /// <summary>底层快照库，恢复界面直接用它查询。</summary>
    public AutoSaveStore Store => _store;

    /// <summary>自上次写入以来是否产生过未落盘的修改。</summary>
    public bool IsDirty
    {
        get { lock (_gate) return _dirty; }
    }

    /// <summary>内容改了一笔：标脏、重置防抖，必要时起一条陈旧上界。</summary>
    public void NotifyEdited()
    {
        if (_disposed) return;
        if (_settings() is not { AutoSaveEnabled: true } settings) return;

        bool armStale;
        lock (_gate)
        {
            if (_suppressed > 0) return;
            _dirty = true;
            // 上界只在本轮第一笔时起，之后不再被输入推后——这正是防抖会饿死的那一环。
            armStale = _stale is null;
        }

        ArmDebounce(settings);
        if (armStale) ArmStale(StaleDelay(settings));
    }

    /// <summary>设置项变了：用新的防抖秒数重排，或干脆停掉计时器。</summary>
    public void ApplySettings()
    {
        if (_disposed) return;

        if (_settings() is not { AutoSaveEnabled: true } settings)
        {
            StopTimers();
            return;
        }

        if (!IsDirty) return;
        ArmDebounce(settings);
        if (_stale is null) ArmStale(StaleDelay(settings));
    }

    /// <summary>
    /// 写一次快照。<paramref name="force"/> 为 <c>false</c>（防抖 / 上界 / 重试触发）时受
    /// 「已禁用 / 暂停中 / 未脏 / 写入间隔未到」四道门控，且正在写就让路；为 <c>true</c>（关键节点）时
    /// 只看内容是否可用，并排队等正在进行的写入结束——关窗时最后一份不能因为「正好在写」丢掉。
    /// 没写成的每一次都会排一次重试，dirty 不会烂在手里。
    /// </summary>
    public async Task FlushAsync(bool force = false)
    {
        if (_disposed) return;
        if (_settings() is not { AutoSaveEnabled: true } settings) return;

        if (force)
            await _writeGate.WaitAsync();
        else if (!_writeGate.Wait(0))
        {
            ScheduleRetry();
            return;
        }

        try
        {
            // 排队后才取，免得 deparse 完才发现要等、写完又是过期内容。
            var folder = _folder();
            var content = _content();

            lock (_gate)
            {
                if (!force)
                {
                    if (_suppressed > 0 || !_dirty) return;
                    if (Environment.TickCount64 - _lastWriteTicks < MinWriteInterval.TotalMilliseconds) return;
                }

                if (folder is null || string.IsNullOrEmpty(content)) return;

                _dirty = false;
            }

            try
            {
                // TargetPath 只用来展示；content:// 上它是 null，恢复时靠 SessionKey / TargetUri。
                var uri = folder.Folder.Path?.ToString();
                var path = folder.FolderLocalPath;

                if (!await _store.SaveAsync(
                        folder.SessionKey, uri, path, content, Math.Max(1, settings.AutoSaveKeep)))
                    lock (_gate) _dirty = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoSaveManager] flush: {ex.Message}");
                lock (_gate) _dirty = true;
            }
            finally
            {
                lock (_gate)
                {
                    _lastWriteTicks = Environment.TickCount64;
                    _stale?.Dispose();
                    _stale = null;
                }
            }
        }
        finally
        {
            _writeGate.Release();
            // 门控拦下、正在写、落盘失败都会走到这里：只要还脏就必须还有下一次。
            if (IsDirty) ScheduleRetry();
        }
    }

    /// <summary>
    /// 切换谱面 / 关窗等节点：停掉全部计时器，脏的话立刻写掉。
    /// 之后的编辑会通过 <see cref="NotifyEdited"/> 自然重新起搏，因此没有对应的 Resume。
    /// </summary>
    public async Task FlushAndSuspendAsync()
    {
        if (_disposed) return;

        StopTimers();
        if (!IsDirty) return;

        await FlushAsync(force: true);
    }

    /// <summary>
    /// 崩溃 / 进程退出路径上的最后一道抢救：同步跑完，不经过 <see cref="DispatcherTimer"/>。
    /// 异常路径上没机会再排重试，所以这里只做一次尽力而为；拿不到写锁说明有写入正在落盘，直接放弃。
    /// 任何失败都吞掉——这个回调里抛异常会让进程连崩溃日志都写不完。
    /// </summary>
    public void FlushEmergency()
    {
        if (_disposed) return;

        try
        {
            if (_settings() is not { AutoSaveEnabled: true } settings) return;
            if (!IsDirty) return;
            if (!_writeGate.Wait(TimeSpan.FromSeconds(2))) return;

            try
            {
                var folder = _folder();
                var content = _content();
                if (folder is null || string.IsNullOrEmpty(content)) return;

                lock (_gate) _dirty = false;

                // 同步等 SQLite 那次后台写入落完，异常路径上不能 await。
                if (!_store.SaveAsync(
                            folder.SessionKey,
                            folder.Folder.Path?.ToString(),
                            folder.FolderLocalPath,
                            content,
                            Math.Max(1, settings.AutoSaveKeep))
                        .GetAwaiter().GetResult())
                    lock (_gate) _dirty = true;
            }
            finally
            {
                lock (_gate) _lastWriteTicks = Environment.TickCount64;
                _writeGate.Release();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AutoSaveManager] emergency flush: {ex.Message}");
            lock (_gate) _dirty = true;
        }
    }
    /// <summary>
    /// 恢复期间压制防抖写入（回写的 maidata 还没读回来，别让快照盖掉现场）。
    /// 用 <c>using</c> 包住恢复流程，Dispose 即恢复，并把压制期间攒下的脏重新排上。
    /// </summary>
    public IDisposable Suppress()
    {
        lock (_gate) _suppressed++;
        return new Suppression(this);
    }

    /// <summary>本轮第一笔改动之后的超时：最迟也要在防抖秒数（上限 <see cref="MaxStale"/>）后写一次。</summary>
    private static TimeSpan StaleDelay(MajEditSetting settings) =>
        TimeSpan.FromSeconds(Math.Min(MaxStale.TotalSeconds, ClampedDebounce(settings)));

    private static double ClampedDebounce(MajEditSetting settings) =>
        Math.Clamp(settings.AutoSaveDebounceSeconds, 1, 120);

    private void ArmDebounce(MajEditSetting settings)
    {
        lock (_gate) _debounce?.Dispose();
        _debounce = DispatcherTimer.RunOnce(() => _ = FlushAsync(), TimeSpan.FromSeconds(ClampedDebounce(settings)));
    }

    private void ArmStale(TimeSpan delay)
    {
        lock (_gate) _stale?.Dispose();
        _stale = DispatcherTimer.RunOnce(() =>
        {
            lock (_gate) _stale = null;
            _ = FlushAsync();
        }, delay);
    }

    /// <summary>排一次稍后的重试；已经在排、不脏或已被压制时什么都不做。</summary>
    private void ScheduleRetry()
    {
        if (_disposed) return;
        lock (_gate)
        {
            if (_retry is not null || !_dirty || _suppressed > 0) return;
            _retry = DispatcherTimer.RunOnce(() =>
            {
                lock (_gate) _retry = null;
                _ = FlushAsync();
            }, RetryInterval);
        }
    }

    private void StopTimers()
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = null;
            _stale?.Dispose();
            _stale = null;
            _retry?.Dispose();
            _retry = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = null;
            _stale?.Dispose();
            _stale = null;
            _retry?.Dispose();
            _retry = null;
        }
        // _writeGate 故意不 Dispose：正在收尾的 flush 还要 Release，SemaphoreSlim 不释放句柄也无害。
        _store.Dispose();
    }

    private sealed class Suppression(AutoSaveManager owner) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            lock (owner._gate) owner._suppressed--;
            // 压制期间攒下的脏没有人起搏过，这里补上。
            owner.ScheduleRetry();
        }
    }
}
