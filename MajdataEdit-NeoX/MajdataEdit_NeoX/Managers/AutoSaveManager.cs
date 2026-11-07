using Avalonia.Threading;
using MajdataEdit_NeoX.Models.IO;
using MajdataEdit_NeoX.Models.Setting;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Managers;

/// <summary>
/// 自动保存调度：编辑防抖 + 关键节点强制 flush。
///
/// 取代 Neo 那份「静态单例 + 60 秒轮询 + IsFileChanged 布尔」：
/// <list type="bullet">
///   <item>没有任何静态状态，谁持有谁用，可测；</item>
///   <item>不是定时轮询，而是「停止输入 N 秒后写」+ 节点强制写，最近的改动先保住；</item>
///   <item>不持有 MainViewModel，只吃三个委托：当前谱面、当前内容、当前设置。</item>
/// </list>
/// 防抖计时器是 <see cref="DispatcherTimer"/>，因此 <c>_content()</c> 的 deparse 都在 UI 线程上跑；
/// 落盘的 SQLite 调用由 <see cref="AutoSaveStore"/> 自己甩到后台。
/// </summary>
public sealed class AutoSaveManager : IDisposable
{
    /// <summary>两次写入之间的硬间隔，防抖被频繁重置时也不至于写爆磁盘。</summary>
    private static readonly TimeSpan MinWriteInterval = TimeSpan.FromSeconds(1);

    private readonly AutoSaveStore _store;
    private readonly Func<ChartFolder?> _folder;
    private readonly Func<string?> _content;
    private readonly Func<MajEditSetting> _settings;

    private readonly object _gate = new();
    private IDisposable? _debounce;
    private bool _dirty;
    private bool _writing;
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

    /// <summary>内容改了一笔：标脏并按当前防抖重新计时。</summary>
    public void NotifyEdited()
    {
        if (_disposed) return;
        if (_settings() is not { AutoSaveEnabled: true } settings) return;

        lock (_gate)
        {
            if (_suppressed > 0) return;
            _dirty = true;
        }

        ArmDebounce(settings);
    }

    /// <summary>设置项变了：用新的防抖秒数重排，或干脆停掉计时器。</summary>
    public void ApplySettings()
    {
        if (_disposed) return;

        if (_settings() is not { AutoSaveEnabled: true } settings)
        {
            _debounce?.Dispose();
            _debounce = null;
            return;
        }

        if (IsDirty) ArmDebounce(settings);
    }

    /// <summary>
    /// 写一次快照。<paramref name="force"/> 为 <c>false</c>（防抖触发）时受
    /// 「已禁用 / 暂停中 / 未脏 / 硬间隔未到」四道门控；为 <c>true</c>（关键节点）时只看内容是否可用。
    /// 正在写时不排队等待，直接把 <see cref="_dirty"/> 留给下一次防抖。
    /// </summary>
    public async Task FlushAsync(bool force = false)
    {
        if (_disposed) return;
        if (_settings() is not { AutoSaveEnabled: true } settings) return;

        var folder = _folder();
        // deparse 要在 UI 线程上做，因此先取内容再进锁后的等待。
        var content = _content();

        lock (_gate)
        {
            if (!force)
            {
                if (_suppressed > 0 || !_dirty || _writing) return;
                if (Environment.TickCount64 - _lastWriteTicks < MinWriteInterval.TotalMilliseconds) return;
            }

            if (_writing) return;
            if (folder is null || string.IsNullOrEmpty(content)) return;

            _writing = true;
            _dirty = false;
        }

        try
        {
            // TargetPath 只用来展示；content:// 上它是 null，恢复时靠 SessionKey / TargetUri。
            var uri = folder.Folder.Path?.ToString();
            var path = folder.FolderLocalPath;

            await _store.SaveAsync(
                folder.SessionKey, uri, path, content, Math.Max(1, settings.AutoSaveKeep));
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
                _writing = false;
                _lastWriteTicks = Environment.TickCount64;
            }
        }
    }

    /// <summary>
    /// 切换谱面 / 关窗等节点：停掉防抖，脏的话立刻写掉。
    /// 之后的编辑会通过 <see cref="NotifyEdited"/> 自然重新起搏，因此没有对应的 Resume。
    /// </summary>
    public async Task FlushAndSuspendAsync()
    {
        if (_disposed) return;

        _debounce?.Dispose();
        if (!IsDirty) return;

        await FlushAsync(force: true);
    }

    /// <summary>
    /// 恢复期间压制防抖写入（回写的 maidata 还没读回来，别让快照盖掉现场）。
    /// 用 <c>using</c> 包住恢复流程，Dispose 即恢复。
    /// </summary>
    public IDisposable Suppress()
    {
        lock (_gate) _suppressed++;
        return new Suppression(this);
    }

    private void ArmDebounce(MajEditSetting settings)
    {
        _debounce?.Dispose();
        var delay = TimeSpan.FromSeconds(Math.Clamp(settings.AutoSaveDebounceSeconds, 1, 120));
        _debounce = DispatcherTimer.RunOnce(() => _ = FlushAsync(), delay);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce?.Dispose();
        _debounce = null;
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
        }
    }
}
