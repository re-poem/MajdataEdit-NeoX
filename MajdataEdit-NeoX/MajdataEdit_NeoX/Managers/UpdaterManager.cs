using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace MajdataEdit_NeoX.Managers;

/// <summary>
/// Velopack 自更新封装。
///
/// 更新源是 GitHub Releases（<see cref="PackId"/> 与打包时的 <c>vpk pack --packId</c> 必须完全一致）。
/// Velopack 只覆盖桌面：Android / iOS / Browser 上 <see cref="IsSupportedPlatform"/> 为 false，调用方据此跳过。
/// 便携包解压出来的目录同样能自更新（这是唯一的分发形态）；只有开发直跑、把 exe 单独挪出来跑
/// 才没有 Velopack 布局，由 <see cref="CanUpdate"/> 拦下给一句提示，而不是让异常冒出来。
/// </summary>
internal sealed class UpdaterManager
{
    /// <summary>包标识。改这里就必须同步改打包脚本里的 <c>--packId</c>，否则永远查不到更新。</summary>
    public const string PackId = "MajdataEdit-NeoX";

    /// <summary>发布面。之所以写死而不是配置项：跟 packId 一样属于构建期约定，用户改不动也不用改。</summary>
    private const string ReleasesRepoUrl = "https://github.com/re-poem/MajdataEdit-NeoX";

    private UpdateManager? _manager;
    private bool _managerCreated;

    public static bool IsSupportedPlatform =>
        OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>
    /// 当前目录是不是一个能自更新的 Velopack 程序目录（便携包解压出来的目录就是）。
    ///
    /// 判据来自 Velopack 的 locator：父目录有 <c>Update.exe</c>、当前目录有 <c>sq.version</c>，
    /// <c>IsInstalled</c> 才有值，<c>IsPortable</c> 另看根目录的 <c>.portable</c> 标记；
    /// 再加上 packId 比对——打包参数和 <see cref="PackId"/> 写岔了就别去查，免得拉到别的项目的包。
    /// </summary>
    public bool CanUpdate =>
        Manager is { } manager
        && (manager.IsInstalled || manager.IsPortable)
        && string.Equals(manager.AppId, PackId, StringComparison.Ordinal);

    /// <summary>是否是便携包跑起来的（只影响提示措辞：便携版更新会原地替换目录）。</summary>
    public bool IsPortable => Manager is { IsPortable: true };

    /// <summary>本地程序版本；非 Velopack 布局为 null。</summary>
    public string? CurrentVersion => Manager?.CurrentVersion?.ToString();

    /// <summary>
    /// 检查更新。没有更新返回 null；已经有「下载完等重启」的包时直接返回它，
    /// 不再打一次网络请求，也不会重复下载。
    /// </summary>
    public async Task<UpdateTarget?> CheckAsync()
    {
        if (Manager is not { } manager || !CanUpdate)
            return null;

        if (manager.UpdatePendingRestart is { } pending)
            return new UpdateTarget(new UpdateInfo(pending, false, null!, []), false);

        var update = await manager.CheckForUpdatesAsync();
        return update is null ? null : new UpdateTarget(update, true);
    }

    /// <summary>下载更新包。<paramref name="progress"/> 收到已下字节数（跟 <see cref="UpdateTarget.SizeBytes"/> 同一单位）。</summary>
    public async Task DownloadAsync(UpdateTarget target, Action<long>? progress = null, CancellationToken cancel = default)
    {
        if (Manager is not { } manager) return;

        // Velopack 只报 0-100 的百分比，这里换算回字节，跟进度窗口的 Progress/Total 对齐。
        var size = target.SizeBytes;
        Action<int>? percent = progress is null
            ? null
            : p => progress(size * Math.Clamp(p, 0, 100) / 100);
        await manager.DownloadUpdatesAsync(target.Info, percent, cancel);
    }

    /// <summary>
    /// 应用已下载的更新并重启进程。调用会先写好更新标记再退出当前进程，
    /// 之后的返回值基本拿不到，所以调用前该落的盘要先落完。
    /// </summary>
    public void ApplyAndRestart(UpdateTarget target)
    {
        if (Manager is { } manager)
            manager.ApplyUpdatesAndRestart(target.Info.TargetFullRelease);
    }

    private UpdateManager? Manager
    {
        get
        {
            if (_managerCreated) return _manager;
            _managerCreated = true;

            if (!IsSupportedPlatform) return null;
            try
            {
                // locator 交给 Velopack 自己探测：安装位置、channel、日志目录都以安装信息为准。
                _manager = new UpdateManager(
                    new GithubSource(ReleasesRepoUrl, accessToken: null, prerelease: false),
                    new UpdateOptions(),
                    null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Updater] init: {ex.Message}");
            }
            return _manager;
        }
    }
}

/// <summary>一次待处理的更新。<see cref="NeedsDownload"/> 为 false 表示包已在本地，只差重启。</summary>
internal sealed record UpdateTarget(UpdateInfo Info, bool NeedsDownload)
{
    public string Version => Info.TargetFullRelease.Version.ToString();

    /// <summary>更新包大小（字节），给进度的「已下 / 总量」用。</summary>
    public long SizeBytes => Info.TargetFullRelease.Size;
}
