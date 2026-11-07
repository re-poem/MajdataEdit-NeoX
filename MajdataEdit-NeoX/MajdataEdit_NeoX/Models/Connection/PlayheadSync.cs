using MajdataEdit_NeoX.Base;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace MajdataEdit_NeoX.Models.Connection;

/// <summary>
/// 编辑器侧的播放头共享内存，与播放器（MajdataViewX 的 <c>TimeProvider</c>）共用
/// <c>majdata_time.dat</c>：写方跟着播放状态走，播放时视图写、编辑器读，
/// 暂停/空闲时反过来。两侧因此共用一个播放头，不必各自跑钟再互相追。
///
/// 布局与视图一致：偏移 0 处一个 <c>float</c>（秒），即视图的 <c>AudioTime</c>，
/// 也就是编辑器这边的 <see cref="ViewModels.MainViewModel.PlayheadTime"/>（含 IncludeOp 的 -5）。
///
/// 打不开（非桌面平台、文件被占、目录只读）时 <see cref="IsAvailable"/> 为 false，
/// 调用方退回本进程的独立 Stopwatch。已经可用之后的读写失败会顺手撤掉映射，
/// 下一轮同样退回独立钟。
/// </summary>
internal sealed class PlayheadSync : IDisposable
{
    /// <summary>视图侧 <c>MemoryMappedViewAccessor.Read(0, out float)</c> 的宽度。</summary>
    private const int Size = sizeof(float);

    /// <summary>
    /// 平台是否值得走共享内存。跨进程共享只在桌面端成立：移动端与 Browser 上编辑器和视图
    /// 的进程空间、文件路径都对不上（Browser 的 BCL 还会直接抛）。
    /// 平台是不变的，所以判一次就够，之后全靠这个字段把 try 挡在外面。
    /// </summary>
    private static readonly bool PlatformSupported =
        !(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsBrowser());

    /// <summary>打开失败后的重试间隔：一次打不开（权限、被占）不必每回播放都赔一次异常。</summary>
    private const long RetryDelayMs = 5_000;

    private FileStream? _file;
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _accessor;
    private long _retryAt; // Environment.TickCount64

    public bool IsAvailable => _accessor is not null;

    /// <summary>
    /// 打开（必要时创建）共享内存，已可用时直接返回 true。
    /// 双方都以 <see cref="FileShare.ReadWrite"/> 打开，谁先启动都能先建出文件。
    /// </summary>
    public bool TryOpen()
    {
        if (IsAvailable) return true;
        if (!PlatformSupported) return false;

        var now = Environment.TickCount64;
        if (now < _retryAt) return false;

        try
        {
            var file = new FileStream(
                MajEnv.MmfAudioTimePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite);
            if (file.Length < Size) file.SetLength(Size);

            _file = file;
            // leaveOpen: true —— 文件句柄归本类，Close 里按「视图 → 映射 → 文件」的顺序撤。
            _map = MemoryMappedFile.CreateFromFile(
                file,
                null,
                Size,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                true);
            _accessor = _map.CreateViewAccessor();
            return true;
        }
        catch (Exception ex)
        {
            _retryAt = now + RetryDelayMs;
            Debug.WriteLine($"[PlayheadSync] open {MajEnv.MmfAudioTimePath}: {ex.Message}");
            Close();
            return false;
        }
    }

    /// <summary>读视图写下的播放头（秒）。</summary>
    public bool TryRead(out double time)
    {
        time = 0;
        if (_accessor is null) return false;

        try
        {
            _accessor.Read(0, out float seconds);
            time = seconds;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayheadSync] read: {ex.Message}");
            Close();
            return false;
        }
    }

    /// <summary>把编辑器持有的播放头（秒）写给视图。</summary>
    public bool TryWrite(double time)
    {
        if (_accessor is null) return false;

        try
        {
            _accessor.Write(0, (float)time);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlayheadSync] write: {ex.Message}");
            Close();
            return false;
        }
    }

    private void Close()
    {
        // 反序拆除：视图、映射、文件。任何一步都不能在后续读写之前留下半开的映射。
        _accessor?.Dispose();
        _accessor = null;
        _map?.Dispose();
        _map = null;
        _file?.Dispose();
        _file = null;
    }

    public void Dispose() => Close();
}
