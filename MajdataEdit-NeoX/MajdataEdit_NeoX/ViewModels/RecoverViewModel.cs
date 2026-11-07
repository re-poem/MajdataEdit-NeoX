using Avalonia.Platform.Storage;
using Avalonia.PropertyGrid.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.Models.IO;
using MajdataEdit_NeoX.Types;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.ViewModels;

/// <summary>恢复列表里的一条快照。标题与显示名在这儿算好，视图只管绑。</summary>
public sealed record RecoverEntry(AutoSaveSnapshot Snapshot, string Title)
{
    public string DisplayName =>
        $"{Title} - {AutoSaveStore.FormatTime(Snapshot.SavedTime)}";
}

/// <summary>快照里可跳转的难度（<c>&amp;inote_N=</c> 的行号）。</summary>
public sealed record RecoverDifficulty(int Index, string DisplayName, int LineNumber);

/// <summary>
/// 自动保存恢复面板的 VM。刻意不认识窗口：目标目录怎么解析、恢复完要不要重载、
/// 出错弹什么，全由构造时注入的委托提供，方便单独跑。
///
/// 候选列表按「当前谱面的本地快照优先、组内按时间倒序」排，全局快照跟随其后。
/// </summary>
public sealed partial class RecoverViewModel : ViewModelBase
{
    private static readonly string[] DifficultyNames =
    [
        "EASY", "BASIC", "ADVANCED", "EXPERT", "MASTER", "Re:MASTER", "ORIGINAL",
    ];

    private readonly AutoSaveStore _store;
    private readonly Func<ChartFolder?> _currentFolder;
    private readonly Func<string, Task<IStorageFolder?>> _resolveFolder;
    private readonly Func<ChartFolder, Task<bool>> _openFolder;
    private readonly Func<IDisposable> _suppress;

    public RecoverViewModel(
        AutoSaveStore store,
        Func<ChartFolder?> currentFolder,
        Func<string, Task<IStorageFolder?>> resolveFolder,
        Func<ChartFolder, Task<bool>> openFolder,
        Func<IDisposable> suppress)
    {
        _store = store;
        _currentFolder = currentFolder;
        _resolveFolder = resolveFolder;
        _openFolder = openFolder;
        _suppress = suppress;
    }

    public ObservableCollection<RecoverEntry> Entries { get; } = new();

    public ObservableCollection<RecoverDifficulty> Difficulties { get; } = new();

    [ObservableProperty]
    public partial RecoverEntry? Selected { get; set; }

    [ObservableProperty]
    public partial string Preview { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(RecoverEntry? value)
    {
        Preview = value?.Snapshot.Content ?? string.Empty;
        Difficulties.Clear();
        foreach (var difficulty in FindDifficulties(Preview))
            Difficulties.Add(difficulty);

        OnPropertyChanged(nameof(HasSelection));
        RecoverCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RecoverCommand.NotifyCanExecuteChanged();
        OpenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>重新拉一次候选。窗口打开时与恢复完都会调用。</summary>
    public async Task RefreshAsync()
    {
        var sessionKey = _currentFolder()?.SessionKey;
        var snapshots = await _store.QueryAsync(200);

        Entries.Clear();
        // 查询本身按时间倒序；当前谱面的快照靠稳定排序提到最前面，其余顺序不变。
        foreach (var snapshot in snapshots.OrderByDescending(snapshot => snapshot.SessionKey == sessionKey))
            Entries.Add(CreateEntry(snapshot));

        Selected = Entries.FirstOrDefault();
    }

    /// <summary>把选中的快照写回它的 maidata.txt（先备份），然后打开那条谱面。</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task RecoverAsync()
    {
        if (Selected is not { } entry) return;
        IsBusy = true;
        // 恢复期间别让防抖写入把现场盖掉。
        using var suppression = _suppress();
        try
        {
            var target = await ResolveTargetAsync(entry);
            if (target is null)
            {
                await FailAsync();
                return;
            }

            try
            {
                await BackupAsync(target);
                await target.WriteMaidataAsync(entry.Snapshot.Content);
            }
            catch
            {
                await FailAsync();
                return;
            }

            await _store.DeleteAsync(entry.Snapshot.Id);

            if (!await _openFolder(target))
            {
                await FailAsync();
                await RefreshAsync();
                return;
            }

            await MessageBox.ShowAsync(
                LocalizationService.Default[LangKeys.Msg_AutoSaveRestored],
                LocalizationService.Default[LangKeys.Gui_Info],
                icon: Icon.Info);
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>不动文件，只打开快照对应的那条谱面。</summary>
    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task OpenAsync()
    {
        if (Selected is not { } entry) return;
        IsBusy = true;
        try
        {
            if (await ResolveTargetAsync(entry) is not { } target || !await _openFolder(target))
                await FailAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanAct() => Selected is not null && !IsBusy;

    /// <summary>恢复失败的统一提示。</summary>
    private static Task FailAsync() => MessageBox.ShowAsync(
        LocalizationService.Default[LangKeys.Msg_RecoverFailed],
        LocalizationService.Default[LangKeys.Gui_Error],
        icon: Icon.Error);

    /// <summary>
    /// 解析回写目标：当前打开的就是同一会话时直接用活句柄（最可靠），
    /// 否则按快照里的 URI 走存储提供者；URI 缺失或失效就放弃。
    /// </summary>
    private async Task<ChartFolder?> ResolveTargetAsync(RecoverEntry entry)
    {
        var current = _currentFolder();
        if (current is not null && current.SessionKey == entry.Snapshot.SessionKey)
            return current;

        if (entry.Snapshot.TargetUri is not { Length: > 0 } uri) return null;

        var folder = await _resolveFolder(uri);
        return folder is null ? null : await ChartFolder.OpenAsync(folder);
    }

    /// <summary>把现有 maidata.txt 留一份 maidata.before_recovery.txt。</summary>
    private static async Task BackupAsync(ChartFolder target)
    {
        string existing;
        try
        {
            existing = await target.ReadMaidataAsync();
        }
        catch
        {
            return; // 读不出来就没有可备份的内容
        }

        if (existing.Length == 0) return;

        var backup = await target.Folder.CreateFileAsync("maidata.before_recovery.txt");
        if (backup is null) return;

        await using var stream = await backup.OpenWriteAsync();
        await stream.WriteAsync(Encoding.UTF8.GetBytes(existing));
    }

    private static RecoverEntry CreateEntry(AutoSaveSnapshot snapshot)
    {
        var title = TitleRegex().Match(snapshot.Content) is { Success: true } match
            ? match.Groups["title"].Value.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(title))
            title = TargetName(snapshot);

        return new RecoverEntry(snapshot, title);
    }

    private static string TargetName(AutoSaveSnapshot snapshot)
    {
        var path = snapshot.TargetPath ?? snapshot.TargetUri ?? string.Empty;
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, '/');
        var name = trimmed.LastIndexOfAny(['/', '\\', ':']);
        return name >= 0 && name + 1 < trimmed.Length ? trimmed[(name + 1)..] : trimmed;
    }

    [GeneratedRegex(@"^[\t ]*&title[\t ]*=(?<title>[^\r\n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"^[\t ]*&inote_(?<index>\d+)[\t ]*=", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex InoteRegex();

    [GeneratedRegex(@"^[\t ]*&lv_(?<index>\d+)[\t ]*=(?<level>[^\r\n]*)", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LevelRegex();

    private static IEnumerable<RecoverDifficulty> FindDifficulties(string content)
    {
        var seen = new HashSet<int>();
        foreach (Match match in InoteRegex().Matches(content))
        {
            var index = int.Parse(match.Groups["index"].Value);
            if (!seen.Add(index)) continue;

            var level = LevelRegex()
                .Match(content, match.Index) is { Success: true } levelMatch && levelMatch.Groups["index"].Value == match.Groups["index"].Value
                ? levelMatch.Groups["level"].Value.Trim()
                : string.Empty;

            var name = index >= 1 && index <= DifficultyNames.Length
                ? DifficultyNames[index - 1]
                : $"DIFFICULTY {index}";
            var display = string.IsNullOrWhiteSpace(level) ? name : $"{name}  {level}";
            var line = content.AsSpan(0, match.Index).Count('\n') + 1;

            yield return new RecoverDifficulty(index, display, line);
        }
    }
}
