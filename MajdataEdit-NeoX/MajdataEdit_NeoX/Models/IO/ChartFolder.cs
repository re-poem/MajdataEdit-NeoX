using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Models.IO;

/// <summary>谱面目录里的媒体文件种类，决定文件名前缀与归档规则。</summary>
public enum ChartMediaKind
{
    /// <summary>背景图，<c>bg.*</c>；归档为 <c>bg-old.*</c>，并且不把 <c>bg.mp4</c> 当封面。</summary>
    Cover,
    /// <summary>背景视频，<c>pv.*</c>；归档为 <c>pv-old.*</c>。</summary>
    Video,
}

/// <summary>
/// 一个谱面目录（<c>maidata.txt</c> + <c>track</c> + <c>bg</c>/<c>pv</c>）的 Storage 句柄视图。
///
/// 谱面的读写全程只经过 <see cref="IStorageFolder"/> / <see cref="IStorageFile"/>，不依赖本地路径：
/// Android 的 <c>content://</c> 与 Browser 的沙箱目录都拿不到本地路径，按路径拼 System.IO 会直接失败。
/// 只有必须把路径交给外部消费者（播放器进程）时才用 <see cref="FolderLocalPath"/> 之类的属性，
/// 取不到就由调用方降级。
/// </summary>
public sealed class ChartFolder
{
    public const string MaidataName = "maidata.txt";

    private static readonly string[] TrackNames = ["track.mp3", "track.ogg"];

    private ChartFolder(IStorageFolder folder, string sessionKey)
    {
        Folder = folder;
        SessionKey = sessionKey;
    }

    public IStorageFolder Folder { get; }

    /// <summary>
    /// 谱面会话的持久标识，用于编辑记录的存取。优先用目录的 bookmark（跨重启稳定），
    /// 平台不支持 bookmark 时退回 <see cref="IStorageItem.Path"/>（Android 上是稳定的 content:// URI）。
    /// </summary>
    public string SessionKey { get; }

    public IStorageFile? Maidata { get; private set; }
    public IStorageFile? Track { get; private set; }
    public IStorageFile? Cover { get; private set; }
    public IStorageFile? Video { get; private set; }

    /// <summary>谱面目录的本地路径；平台上是非文件 URI（Android 的 <c>content://</c>、Browser 沙箱）时为 <c>null</c>。</summary>
    public string? FolderLocalPath => LocalPathOf(Folder);
    public string? TrackLocalPath => LocalPathOf(Track);
    public string? CoverLocalPath => LocalPathOf(Cover);
    public string? VideoLocalPath => LocalPathOf(Video);

    /// <summary>
    /// 取存储项在本地文件系统上的路径，供确实需要路径的消费者（外部播放器进程等）使用。
    /// 只认 <c>file:</c> URI；其余 scheme 没有本地路径，返回 <c>null</c> 由调用方降级。
    /// </summary>
    private static string? LocalPathOf(IStorageItem? item)
        => item?.Path is { IsFile: true } uri ? uri.LocalPath : null;

    /// <summary>
    /// 打开一个谱面目录。<paramref name="createMaidata"/> 为 true 时（新建谱面）
    /// 会在缺失时创建一个空的 <c>maidata.txt</c>。
    /// </summary>
    /// <returns>目录里缺少 maidata.txt 或 track 时返回 <c>null</c>。</returns>
    public static async Task<ChartFolder?> OpenAsync(IStorageFolder folder, bool createMaidata = false)
    {
        var sessionKey = await GetSessionKeyAsync(folder);
        var chart = new ChartFolder(folder, sessionKey);

        chart.Maidata = await GetFileAsync(folder, MaidataName);
        if (chart.Maidata is null)
        {
            if (!createMaidata) return null;
            chart.Maidata = await folder.CreateFileAsync(MaidataName);
        }

        await chart.ScanAsync();
        return chart.Track is null ? null : chart;
    }

    /// <summary>重新扫描目录内容（重载谱面时用）。</summary>
    public async Task RefreshAsync()
    {
        Maidata = await GetFileAsync(Folder, MaidataName);
        await ScanAsync();
    }

    public async Task<string> ReadMaidataAsync()
    {
        var file = Maidata ?? throw new InvalidOperationException("maidata.txt is not available.");
        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    public async Task WriteMaidataAsync(string text)
    {
        // 用 CreateFileAsync 而不是直接 OpenWriteAsync：前者会把已存在的文件截断，
        // 否则写短内容时旧内容的尾巴会留在文件里。
        var file = await Folder.CreateFileAsync(MaidataName)
            ?? throw new IOException($"Cannot create {MaidataName}.");
        await using (var stream = await file.OpenWriteAsync())
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(text));
        }
        Maidata = file;
    }

    /// <summary>
    /// 把 <paramref name="source"/> 导入为 <c>bg.&lt;ext&gt;</c> / <c>pv.&lt;ext&gt;</c>，
    /// 目录里同前缀的旧文件先归档成 <c>bg-old.&lt;ext&gt;</c> / <c>pv-old.&lt;ext&gt;</c>。
    /// </summary>
    public async Task<IStorageFile> ImportAsync(IStorageFile source, ChartMediaKind kind)
    {
        var prefix = PrefixOf(kind);
        var extension = GetExtension(source.Name);
        if (string.IsNullOrEmpty(extension))
            throw new ArgumentException("Source file has no extension.", nameof(source));

        // 先落到临时文件再归档：源文件可能就是目录里现有的 bg.*，直接归档会把它删掉。
        var tempName = $".{prefix}-import-{Guid.NewGuid():N}.tmp";
        var temp = await Folder.CreateFileAsync(tempName)
            ?? throw new IOException($"Cannot create {tempName}.");
        try
        {
            await CopyAsync(source, temp);

            // 归档旧文件。归档到同名 <prefix>-old.<ext> 时会覆盖更早的归档，与旧行为一致。
            foreach (var item in await GetItemsAsync(Folder))
            {
                if (item is not IStorageFile existing) continue;
                if (!MatchesMedia(existing.Name, prefix, kind)) continue;

                var archive = await Folder.CreateFileAsync($"{prefix}-old{GetExtension(existing.Name)}")
                    ?? throw new IOException("Cannot create archive file.");
                await CopyAsync(existing, archive);
                await existing.DeleteAsync();
            }

            var destination = await Folder.CreateFileAsync(prefix + extension)
                ?? throw new IOException($"Cannot create {prefix + extension}.");
            await CopyAsync(temp, destination);

            await RefreshAsync();
            return destination;
        }
        finally
        {
            try { await temp.DeleteAsync(); } catch { /* 清不掉临时文件不影响导入结果 */ }
        }
    }

    private async Task ScanAsync()
    {
        Track = null;
        foreach (var name in TrackNames)
        {
            Track = await GetFileAsync(Folder, name);
            if (Track is not null) break;
        }

        var files = (await GetItemsAsync(Folder)).OfType<IStorageFile>().ToArray();
        Cover = files.FirstOrDefault(file => MatchesMedia(file.Name, PrefixOf(ChartMediaKind.Cover), ChartMediaKind.Cover));
        Video = files.FirstOrDefault(file => MatchesMedia(file.Name, PrefixOf(ChartMediaKind.Video), ChartMediaKind.Video))
            ?? await GetFileAsync(Folder, "bg.mp4");
    }

    private static string PrefixOf(ChartMediaKind kind) =>
        kind == ChartMediaKind.Cover ? "bg" : "pv";

    private static string GetExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? string.Empty : name[dot..];
    }

    /// <summary>
    /// <c>bg.*</c> / <c>pv.*</c> 的匹配规则：排除已归档的 <c>-old</c> 前缀，
    /// 封面还要排除视频文件（<c>bg.mp4</c> 是背景视频而不是封面）。
    /// </summary>
    private static bool MatchesMedia(string name, string prefix, ChartMediaKind kind)
    {
        if (!name.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.StartsWith(prefix + "-old.", StringComparison.OrdinalIgnoreCase)) return false;
        return kind != ChartMediaKind.Cover ||
               !name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetSessionKeyAsync(IStorageItem item)
    {
        if (item.CanBookmark)
        {
            try
            {
                if (await item.SaveBookmarkAsync() is { Length: > 0 } bookmark)
                    return "bookmark:" + bookmark;
            }
            catch
            {
                // bookmark 被系统拒绝时退回 Path
            }
        }

        return item.Path?.ToString() ?? item.Name;
    }

    private static async Task<IStorageFile?> GetFileAsync(IStorageFolder folder, string name)
    {
        try
        {
            return await folder.GetFileAsync(name);
        }
        catch
        {
            // 不存在的文件在不同平台上的表现不一致（null / 异常），统一按"没有"处理
            return null;
        }
    }

    private static async Task<IReadOnlyList<IStorageItem>> GetItemsAsync(IStorageFolder folder)
    {
        var items = new List<IStorageItem>();
        try
        {
            await foreach (var item in folder.GetItemsAsync())
                items.Add(item);
        }
        catch
        {
            // 目录不可枚举（权限被收回等）时按"空目录"处理，调用方各自降级
        }
        return items;
    }

    private static async Task CopyAsync(IStorageFile source, IStorageFile destination)
    {
        await using var input = await source.OpenReadAsync();
        await using var output = await destination.OpenWriteAsync();
        await input.CopyToAsync(output);
    }
}
