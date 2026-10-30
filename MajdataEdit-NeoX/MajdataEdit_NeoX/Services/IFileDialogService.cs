using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Services;

public interface IFileDialogService
{
    /// <summary>
    /// 弹出"打开文件"对话框。返回用户选择的存储项；用户取消返回 <c>null</c>。
    ///
    /// 返回 <see cref="IStorageFile"/> 而不是路径：Android 的 <c>content://</c> 与
    /// Browser 沙箱都没有可用的本地路径，读写必须走存储项自己的流。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="filters">文件类型过滤器（按声明顺序显示在下拉框里）。</param>
    Task<IStorageFile?> OpenFileAsync(string title, IReadOnlyList<FileDialogFilter> filters);

    /// <summary>
    /// 弹出"另存为"对话框。返回用户选择的存储项；用户取消返回 <c>null</c>。
    /// </summary>
    /// <param name="title">对话框标题。</param>
    /// <param name="defaultFileName">建议的默认文件名（不含扩展名）。可空。</param>
    /// <param name="filters">文件类型过滤器（按声明顺序显示在下拉框里）。可空。</param>
    Task<IStorageFile?> SaveFileAsync(
        string title,
        string? defaultFileName = null,
        IReadOnlyList<FileDialogFilter>? filters = null);
}

/// <summary>
/// 一个文件类型过滤器项。Maps 到 Avalonia 的 <c>FilePickerFileType</c>。
/// 平台无关，避免把 Avalonia 类型泄漏到接口调用方。
/// </summary>
public sealed record FileDialogFilter(string Name, IReadOnlyList<string> Patterns);
