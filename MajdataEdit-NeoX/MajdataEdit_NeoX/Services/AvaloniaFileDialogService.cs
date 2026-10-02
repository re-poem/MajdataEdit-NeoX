using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Services;

/// <summary>
/// <see cref="IFileDialogService"/> 的 Avalonia 实现。运行时按需通过
/// <see cref="Func{TResult}"/> 拿 <c>MainWindow.StorageProvider</c>。
///
/// **不在构造时持有 provider**，因为 <c>MainWindow</c> 是在 <c>App.OnFrameworkInitializationCompleted</c>
/// 阶段才被建出来的，早于 VM 构造，但 <c>StorageProvider</c> 是窗口挂到 visual tree 之后才可用。
/// 所以这里只持有一个能"现在去取"的 <see cref="Func{TResult}"/>，每次调用时拿最新的 provider。
/// </summary>
public sealed class AvaloniaFileDialogService(Func<IStorageProvider?> storageProviderAccessor) : IFileDialogService
{
    public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FileDialogFilter> filters)
    {
        var provider = storageProviderAccessor();
        if (provider is null) return null;

        var pickerOptions = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
                .Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns.ToList() })
                .ToList(),
        };

        var files = await provider.OpenFilePickerAsync(pickerOptions);
        return files.Count >= 1 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveFileAsync(
        string title,
        string? defaultFileName = null,
        IReadOnlyList<FileDialogFilter>? filters = null)
    {
        var provider = storageProviderAccessor();
        if (provider is null) return null;

        var pickerOptions = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = defaultFileName,
            FileTypeChoices = filters?
                .Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns.ToList() })
                .ToList(),
        };

        var file = await provider.SaveFilePickerAsync(pickerOptions);
        return file?.TryGetLocalPath();
    }
}