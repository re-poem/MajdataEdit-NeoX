using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.PropertyGrid.Services;
using Avalonia.Threading;
using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using Dock.Serializer.SystemTextJson;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.Models.Connection;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Models.IO;
using MajdataEdit_NeoX.Models.Plugin;
using MajdataEdit_NeoX.Models.Setting;
using MajdataEdit_NeoX.Models.Track;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.Views;
using PropertyModels.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    // Design
    public MainViewModel() : this(new AvaloniaFileDialogService(() => null), new AvaloniaMessageService(() => null)) { }

    private readonly IFileDialogService _fileDialog;
    private readonly IMessageService _message;
    public IEditorService? Editor { get; set; }
    public MainViewModel(IFileDialogService fileDialog, IMessageService message)
    {
        _fileDialog = fileDialog;
        _message = message;

        LoadSettings();
        LocalizationService.Default.SelectCulture(Language);

        _player = new PlayerConnection(Settings.EditSetting.PlayerIps);
        _player.OnConnectedChanged += connected =>
        {
            _pushedSetting = false;
            _chartDirty = true;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ActiveConnectionCount));
            _ = SynchronizeConnectedPlayersAsync();
        };

        _chartEditMng = new(MajEnv.DatabaseFile);

        _pluginsMng = new();
        _pluginsMng.OnPluginsChanged += () =>
        {
            OnPropertyChanged(nameof(Plugins));
            RebuildPluginShortcuts();
        };

        RegisterShortcuts();

        _timelineTimer.Tick += (_, _) =>
        {
            if (!_player.IsConnected) return;
            if (_viewState is not ViewStatus.Playing) return;
            PlayheadTime = _timelineOrigin + _timelineClock.Elapsed.TotalSeconds * _timelineSpeed;
        };
        _timelineTimer.Start();

        Factory = new MainDockFactory(this);
        Layout = LoadLayout() ?? Factory.CreateLayout()!;
        Factory.InitLayout(Layout);
    }



    #region Layout

    public MainDockFactory Factory { get; } = null!;
    private IRootDock _layout = null!;
    public IRootDock Layout
    {
        get => _layout;
        set
        {
            if (ReferenceEquals(_layout, value)) return;
            _layout = value;
            OnPropertyChanged(nameof(Layout));
        }
    }

    private void SaveLayout()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MajEnv.LayoutFile)!);
            using var stream = File.Create(MajEnv.LayoutFile);
            new DockSerializer().Save(stream, Layout);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save dock layout: {ex.Message}");
        }
    }

    private IRootDock? LoadLayout()
    {
        if (!File.Exists(MajEnv.LayoutFile)) return null;

        try
        {
            using var stream = File.OpenRead(MajEnv.LayoutFile);
            return new DockSerializer().Load<IRootDock?>(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load dock layout: {ex.Message}");
            return null;
        }
    }

    #endregion



    #region 设置

    // 全局唯一，不要直接修改内部项，用转发的property修改
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Language))]
    [NotifyPropertyChangedFor(nameof(FontSize))]
    [NotifyPropertyChangedFor(nameof(WaveAnimated))]
    [NotifyPropertyChangedFor(nameof(WordWrap))]
    [NotifyPropertyChangedFor(nameof(BackgroundImagePath))]
    public partial MajSetting Settings { get; set; } = new();

    /// <summary>
    /// 设置文件的序列化选项：缩进排版，枚举写成数字（与既有 Settings.json 保持一致）。
    /// </summary>
    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        WriteIndented = true,
    };

    public void LoadSettings()
    {
        try
        {
            if (File.Exists(MajEnv.SettingsFile))
            {
                var json = File.ReadAllText(MajEnv.SettingsFile);
                // 集合属性（List<string>）要整体替换而不是往默认实例里追加，
                // 这里靠 MajSetting 上的 [JsonObjectCreationHandling(Replace)] 保证。
                Settings = JsonSerializer.Deserialize<MajSetting>(json, SettingsJsonOptions) ?? new();
            }
            else
            {
                Settings = new MajSetting();
                SaveSettings(); // Save defaults
            }
        }
        catch (Exception ex)
        {
            // If load fails, use defaults
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            Settings = new MajSetting();
        }
    }

    public void SaveSettings()
    {
        try
        {
            if (Settings == null)
                return;

            var directory = Path.GetDirectoryName(MajEnv.SettingsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Settings, SettingsJsonOptions);
            File.WriteAllText(MajEnv.SettingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
            throw;
        }
    }

    private static readonly HashSet<string> ViewSettingPropertyNames = typeof(MajViewSetting).GetProperties().Select(o => o.Name).ToHashSet();
    private static readonly HashSet<string> VolumeSettingPropertyNames = typeof(MajVolumeSetting).GetProperties().Select(o => o.Name).ToHashSet();
    // PropertyGrid 修改设置时，通过此入口提醒设置变更。
    [RelayCommand]
    public void NotifySettingsChanged(string settingName)
    {
        switch (settingName)
        {
            case nameof(MajSetting.EditSetting.PlayerIps):
                _ = _player.SetPlayersAsync(Settings.EditSetting.PlayerIps);
                break;
            case nameof(MajSetting.EditSetting.Language):
                LocalizationService.Default.SelectCulture(Language);
                break;
        }
        if (ViewSettingPropertyNames.Contains(settingName) ||
            VolumeSettingPropertyNames.Contains(settingName))
        {
            _pushedSetting = false;
            _ = EnsurePlayableAsync();
        }

        OnPropertyChanged(settingName);
        SaveSettings();
    }

    // 模板代码，对设置项进行展开和提醒

    public string Language
    {
        get => Settings.EditSetting.Language;
        set
        {
            if (Settings.EditSetting.Language.Equals(value))
                return;

            Settings.EditSetting.Language = value;
            OnPropertyChanged(nameof(Language));
        }
    }

    public float FontSize
    {
        get => Settings.EditSetting.FontSize;
        set
        {
            if (Settings.EditSetting.FontSize.Equals(value))
                return;

            Settings.EditSetting.FontSize = value;
            OnPropertyChanged(nameof(FontSize));
        }
    }

    public bool WaveAnimated
    {
        get => Settings.EditSetting.WaveAnimated;
        set
        {
            if (Settings.EditSetting.WaveAnimated.Equals(value))
                return;

            Settings.EditSetting.WaveAnimated = value;
            OnPropertyChanged(nameof(WaveAnimated));
        }
    }

    public bool WordWrap
    {
        get => Settings.EditSetting.WordWrap;
        set
        {
            if (Settings.EditSetting.WordWrap.Equals(value))
                return;

            Settings.EditSetting.WordWrap = value;
            OnPropertyChanged(nameof(WordWrap));
        }
    }

    public string BackgroundImagePath
    {
        get => Settings.EditSetting.BackgroundImagePath;
        set
        {
            if (Settings.EditSetting.BackgroundImagePath.Equals(value))
                return;

            Settings.EditSetting.BackgroundImagePath = value;
            OnPropertyChanged(nameof(BackgroundImagePath));
        }
    }
    public double BackgroundImageOpacity
    {
        get => Settings.EditSetting.BackgroundImageOpacity;
        set
        {
            if (Settings.EditSetting.BackgroundImageOpacity.Equals(value))
                return;

            Settings.EditSetting.BackgroundImageOpacity = value;
            OnPropertyChanged(nameof(BackgroundImageOpacity));
        }
    }
    public double BackgroundBlurRadius
    {
        get => Settings.EditSetting.BackgroundBlurRadius;
        set
        {
            if (Settings.EditSetting.BackgroundBlurRadius.Equals(value))
                return;

            Settings.EditSetting.BackgroundBlurRadius = value;
            OnPropertyChanged(nameof(BackgroundBlurRadius));
        }
    }
    public double BackgroundOffsetX
    {
        get => Settings.EditSetting.BackgroundOffsetX;
        set
        {
            if (Settings.EditSetting.BackgroundOffsetX.Equals(value))
                return;

            Settings.EditSetting.BackgroundOffsetX = value;
            OnPropertyChanged(nameof(BackgroundOffsetX));
        }
    }
    public double BackgroundOffsetY
    {
        get => Settings.EditSetting.BackgroundOffsetY;
        set
        {
            if (Settings.EditSetting.BackgroundOffsetY.Equals(value))
                return;

            Settings.EditSetting.BackgroundOffsetY = value;
            OnPropertyChanged(nameof(BackgroundOffsetY));
        }
    }
    public Stretch BackgroundStretch
    {
        get => Settings.EditSetting.BackgroundStretch;
        set
        {
            if (Settings.EditSetting.BackgroundStretch.Equals(value))
                return;

            Settings.EditSetting.BackgroundStretch = value;
            OnPropertyChanged(nameof(BackgroundStretch));
        }
    }

    #endregion

    #region 文件

    [ObservableProperty]
    // 文件变化刷新
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))] // Title与WindowTitle绑定
    [NotifyPropertyChangedFor(nameof(Artist))]
    [NotifyPropertyChangedFor(nameof(FinalDesigner))]
    [NotifyPropertyChangedFor(nameof(Offset))]
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(Fumen))]
    [NotifyPropertyChangedFor(nameof(Chart))]
    public partial MaidataFile? Maidata { get; private set; }
    partial void OnMaidataChanged(MaidataFile? oldValue, MaidataFile? newValue)
    {
        oldValue?.PropertyChanged -= Maidata_PropertyChanged;
        newValue?.PropertyChanged += Maidata_PropertyChanged;
        if (newValue is not null && _chartFolder is not null)
            LoadChartEditRecord(_chartFolder.SessionKey);
        _ = ParseAsync();
    }
    private void Maidata_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // 内部修改刷新
        switch (e.PropertyName)
        {
            case nameof(MaidataFile.Title):
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(WindowTitle)); // Title与WindowTitle绑定
                break;
            case nameof(MaidataFile.Artist):
                OnPropertyChanged(nameof(Artist));
                break;
            case nameof(MaidataFile.Designer):
                OnPropertyChanged(nameof(FinalDesigner));
                break;
            case nameof(MaidataFile.Offset):
                OnPropertyChanged(nameof(Offset));
                break;
            case nameof(MaidataFile.Levels):
                OnPropertyChanged(nameof(Level));
                break;
            case nameof(MaidataFile.Designers):
                OnPropertyChanged(nameof(Designer));
                break;
            case nameof(MaidataFile.Fumens):
                OnPropertyChanged(nameof(Fumen));
                break;
            case nameof(MaidataFile.Charts):
                OnPropertyChanged(nameof(Chart));
                break;
            default:
                break;
        }

        // MaidataFile 任意更改都让服务端重新 Parse（谱面 + metadata 一次提交）。
        _ = ParseAsync();
    }

    // ========编辑态========
    [ObservableProperty]
    // 切换难度刷新
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(Fumen))]
    [NotifyPropertyChangedFor(nameof(Chart))]
    public partial int SelectedDifficulty { get; set; }
    partial void OnSelectedDifficultyChanged(int value) => _ = ParseAsync();

    // 此处对参数进行展开，语义统一，也方便绑定。
    // 不需要OnPropertyChanged，Maidata_PropertyChanged内已经处理。

    public string Title
    {
        get => Maidata?.Title ?? string.Empty;
        set => Maidata?.Title = value;
    }
    public string Artist
    {
        get => Maidata?.Artist ?? string.Empty;
        set => Maidata?.Artist = value;
    }
    public string FinalDesigner
    {
        get => Maidata?.Designer ?? string.Empty;
        set => Maidata?.Designer = value;
    }
    public float Offset
    {
        get => Maidata?.Offset ?? 0f;
        set => Maidata?.Offset = value;
    }
    public string? Level
    {
        get => Maidata?.Levels[SelectedDifficulty];
        set => Maidata?.Levels[SelectedDifficulty] = value;
    }
    public string? Designer
    {
        get => Maidata?.Designers[SelectedDifficulty];
        set => Maidata?.Designers[SelectedDifficulty] = value;
    }
    public string? Fumen
    {
        get => Maidata?.Fumens[SelectedDifficulty];
        set => Maidata?.Fumens[SelectedDifficulty] = value;
    }
    public SimaiChart? Chart
    {
        get => Maidata is { } m ? m.Charts[SelectedDifficulty] : null;
    }


    // ======外围（一般来说跟着Maidata一起变化）

    [RelayCommand]
    private async Task ImportBgCoverAsync(IStorageFile? source)
    {
        if (_chartFolder is null || source is null) return;

        await _chartFolder.ImportAsync(source, ChartMediaKind.Cover);
        await UpdateResourcesAsync();
        await PushResourcesToPlayerAsync();
    }

    [RelayCommand]
    private async Task ImportBgVideoAsync(IStorageFile? source)
    {
        if (_chartFolder is null || source is null) return;

        await _chartFolder.ImportAsync(source, ChartMediaKind.Video);
        await UpdateResourcesAsync();
        await PushResourcesToPlayerAsync();
    }

    /// <summary>
    /// 打开一个谱面目录（<c>maidata.txt</c> + <c>track</c> + <c>bg</c>/<c>pv</c>）。
    /// </summary>
    /// <param name="createMaidata">新建谱面时允许就地创建空的 <c>maidata.txt</c>。</param>
    /// <returns>目录里没有 track 时返回 <c>null</c>，且不改动当前已打开的谱面。</returns>
    private async Task<ChartFolder?> OpenChartFolderAsync(IStorageFolder folder, bool createMaidata = false)
    {
        var chart = await ChartFolder.OpenAsync(folder, createMaidata);
        if (chart is null) return null;

        SaveChartEditRecord();
        _chartFolder = chart;
        _recordedChartPath = null;
        _chartEditTimer.Reset();
        return chart;
    }

    private ChartFolder? _chartFolder;

    private async Task UpdateResourcesAsync()
    {
        TrackInfo = await ReadTrackInfoAsync(_chartFolder?.Track);
        Cover = await ReadCoverAsync(_chartFolder?.Cover);
    }

    /// <summary>
    /// 读取音频波形信息。存储项一律读进内存再交给 ffmpeg 解码：
    /// 内存解码对拿不到本地路径的存储项同样适用，桌面端与 Android / Browser 行为一致。
    /// </summary>
    private static async Task<TrackInfo?> ReadTrackInfoAsync(IStorageFile? file)
    {
        if (file is null) return null;

        var data = await ReadAllBytesAsync(file);
        try
        {
            return await Task.Run(() => TrackReader.ReadTrack(data));
        }
        catch { return null; /* 吞了 */ }
    }

    /// <summary>
    /// 读取封面位图。同样统一从流解码，不依赖存储项是否落在本地文件系统上。
    /// </summary>
    private static async Task<Bitmap?> ReadCoverAsync(IStorageFile? file)
    {
        if (file is null) return null;

        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        buffer.Position = 0;
        return new Bitmap(buffer);
    }

    private static async Task<byte[]> ReadAllBytesAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// 把当前的 track/bg/pv 推给播放器。播放器侧要的是它自己能打开的路径，
    /// 因此这里取存储项的 <see cref="IStorageItem.Path"/> 的本地路径；
    /// 拿不到（Android 的 <c>content://</c>、Browser 沙箱）时传空串，
    /// 这是协议约定的「该字段无法提供」的表达方式。
    /// </summary>
    private async Task PushResourcesToPlayerAsync()
    {
        if (!_player.IsConnected) return;
        if (_chartFolder is not { Track: not null } folder) return;

        await _player.FilesAsync(
            folder.TrackLocalPath ?? string.Empty,
            folder.CoverLocalPath ?? string.Empty,
            folder.VideoLocalPath ?? string.Empty);
    }

    private void LoadChartEditRecord(string chartKey)
    {
        var record = _chartEditMng.GetRecord(chartKey);
        _recordedChartPath = chartKey;
        SelectedDifficulty = record?.SelectedDifficulty ?? 0;
        PlayheadTime = record?.TrackTime ?? 0;
        _chartEditTimer.LoadAccumulated(record?.TotalEditDuration ?? TimeSpan.Zero);
        _chartEditTimer.Start();
    }

    private void SaveChartEditRecord()
    {
        if (_recordedChartPath is null) return;

        _chartEditTimer.Pause();
        _chartEditMng.UpsertRecord(new ChartEditRecord(
            SelectedDifficulty,
            PlayheadTime,
            _chartEditTimer.Elapsed,
            _recordedChartPath));
    }


    [ObservableProperty]
    public partial TrackInfo? TrackInfo { get; private set; }

    [ObservableProperty]
    public partial Bitmap? Cover { get; private set; }

    #endregion



    // ========临时态========

    // 窗口

    public string WindowTitle
    {
        get
        {
            if (Maidata is null)
                return "MajdataEdit-NeoX";
            else
            {
                if (Maidata.IsDirty)
                    return $"{Maidata.Title}*";
                else
                    return $"{Maidata.Title}";
            }
        }
    }


    // 播放/UI

    [ObservableProperty] public partial int CaretOffset { get; set; }
    [ObservableProperty] public partial double CaretTime { get; set; }
    [ObservableProperty] public partial int CaretCombo { get; set; }
    [ObservableProperty] public partial double PlayheadTime { get; set; }

    [ObservableProperty] public partial float PlaybackSpeed { get; set; } = 1f;
    [ObservableProperty] public partial bool IsFollowCursor { get; set; }


    [ObservableProperty] public partial float ZoomLevel { get; set; } = 1f;



    // 连接

    // 变化通知在构造函数 _player.OnConnectedChanged
    // 因为直接绑的 _client.State，没法 Observable
    public bool IsConnected => _player.IsConnected;
    public int ActiveConnectionCount => _player.ActiveConnectionCount;

    private readonly PlayerConnection _player;
    // 播放态由编辑器自己的命令结果驱动（命令会等到播放器响应），不再从连接层回调。
    private ViewStatus _viewState = ViewStatus.Idle;

    private bool _chartDirty; // 谱面解析被跳过（断连/播放中），待连接且空闲后补推
    private bool _pushedSetting; // 在任一连接完成之后，有至少一次settings提交
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    // 播放期间的时间线跟踪：本地 Stopwatch 推算（origin + elapsed × speed），
    private readonly Stopwatch _timelineClock = new();
    private double _timelineOrigin;
    private float _timelineSpeed = 1f;
    // 常驻 UI 时钟：非播放状态的 tick 直接跳过，因此不需要启停控制和跨线程取消那套机器。
    private readonly DispatcherTimer _timelineTimer = new() { Interval = TimeSpan.FromMilliseconds(16.666667f) };
    private double? _lastPlayTime; // 供Stop进行回退

    // 编辑记录
    private readonly ChartEditManager _chartEditMng;
    private readonly ChartEditTimer _chartEditTimer = new();
    private string? _recordedChartPath;


    // Plugins
    private readonly PluginManager _pluginsMng;
    public IReadOnlyList<LuaPlugin> Plugins => _pluginsMng.Plugins;

    #region Commands - 窗口

    // 选择谱面用的过滤器。Android 的选择器只认 MIME，见 FileDialogFilter。
    private static readonly FileDialogFilter[] TrackFileFilter =
        [new FileDialogFilter("Track Files", ["track.mp3", "track.ogg"], ["audio/mpeg", "audio/ogg"])];
    private static readonly FileDialogFilter[] MaidataFileFilter =
        [new FileDialogFilter("Majdata Files", ["maidata.txt"], ["text/plain"])];

    /// <summary>
    /// 平台是否可靠支持"选中文件 → 取它的父目录"。
    ///
    /// Android 的 <c>content://</c> 上 <c>GetParentAsync</c> 会返回
    /// <c>file:///document/…</c> 这种文件系统里不存在的目录（枚举时
    /// <c>DocumentsContract.GetTreeDocumentId</c> 直接抛异常），Browser 的实现恒返回
    /// <c>null</c>，两者都改成让用户直接选目录。桌面与 iOS 保持选文件。
    /// 可写，方便测试/诊断强制走某一条分支。
    /// </summary>
    public static bool PreferFolderPicker { get; set; } =
        OperatingSystem.IsAndroid() || OperatingSystem.IsBrowser();

    /// <summary>
    /// 选一个谱面目录：支持父目录的平台由所选文件换算，其余平台直接选目录。
    /// 用户取消返回 <c>null</c>；换算不出目录时提示后返回 <c>null</c>。
    /// </summary>
    private async Task<IStorageFolder?> PickChartFolderAsync(string title, IReadOnlyList<FileDialogFilter> filters)
    {
        if (PreferFolderPicker)
            return await _fileDialog.OpenFolderAsync(title);

        var file = await _fileDialog.OpenFileAsync(title, filters);
        if (file is null) return null;

        var folder = await file.GetParentAsync();
        if (folder is null)
            await MessageErrorAsync(LangKeys.Gui_NoParentFolder);
        return folder;
    }

    [RelayCommand]
    public async Task NewFileAsync()
    {
        // 谱面 = track 所在的目录。目录里没有 maidata.txt 时就地建一个空文件。
        var folder = await PickChartFolderAsync(LocalizationService.Default[LangKeys.Gui_Open], TrackFileFilter);
        if (folder is null) return;

        var chart = await OpenChartFolderAsync(folder, createMaidata: true);
        if (chart is null)
        {
            await MessageErrorAsync(LangKeys.Gui_NoTrackFile);
            return;
        }

        Maidata = MaidataFile.New();

        await UpdateResourcesAsync();
        await LoadAsync();
    }
    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var folder = await PickChartFolderAsync(LocalizationService.Default[LangKeys.Gui_Open], MaidataFileFilter);
        if (folder is null) return;

        var chart = await OpenChartFolderAsync(folder);
        if (chart is null)
        {
            await MessageErrorAsync(LangKeys.Gui_NoTrackFile);
            return;
        }

        Maidata = MaidataFile.Parse(await chart.ReadMaidataAsync());
        await UpdateResourcesAsync();

        await LoadAsync();
    }
    [RelayCommand]
    public async Task SaveFileAsync()
    {
        if (_chartFolder is null) return;
        if (Maidata is null) return;

        var text = MaidataFile.Deparse(Maidata);
        await _chartFolder.WriteMaidataAsync(text);
        Maidata.MarkAsSaved();
    }
    [RelayCommand]
    public async Task ReloadFileAsync()
    {
        if (_chartFolder is null) return;

        // OpenChartFolderAsync 内部会重新扫描目录，无需先 Refresh。
        var chart = await OpenChartFolderAsync(_chartFolder.Folder);
        if (chart is null) return;

        Maidata = MaidataFile.Parse(await chart.ReadMaidataAsync());
        await UpdateResourcesAsync();
        await LoadAsync();
    }




    [RelayCommand]
    public void OpenRecover()
    {

    }
    [RelayCommand]
    public void OpenChartInfo()
    {
        Factory.FloatOrActivateTool<ChartInfoTool>();
    }
    [RelayCommand]
    public void OpenSettings()
    {
        Factory.FloatOrActivateTool<SettingsTool>();
    }
    [RelayCommand]
    public void OpenBpmTap()
    {

    }
    [RelayCommand]
    public void OpenShortcut()
    {
        Factory.FloatOrActivateTool<ShortcutTool>();
    }
    [RelayCommand]
    public void OpenAbout(AboutPages page)
    {
        switch (page)
        {
            case AboutPages.Discord: OpenBrowser("https://discord.gg/AcWgZN7j6K"); break;
            case AboutPages.QQ: OpenBrowser("https://qm.qq.com/q/GAxbFZHP6A"); break;
            case AboutPages.GitHub: OpenBrowser("https://github.com/LingFeng-bbben/MajdataEdit-Neo"); break;
            case AboutPages.GitHub_ViewX: OpenBrowser("https://github.com/re-poem/MajdataViewX"); break;
            case AboutPages.MajNet: OpenBrowser("https://discord.gg/Majnet"); break;
        }
        static void OpenBrowser(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }





    [RelayCommand]
    public void ExecutePluginAction(PluginAction? action)
    {
        var input = Editor?.GetSelectedText();
        if (input == null) return;
        var result = action?.Transform.Invoke(input);
        if (result == null) return;
        Editor?.SetSelectedText(result);
    }

    [RelayCommand]
    public void IncreaseFontSize() => StepFontSize(+1);
    [RelayCommand]
    public void DecreaseFontSize() => StepFontSize(-1);
    private void StepFontSize(float delta)
    {
        FontSize = Math.Clamp(FontSize + delta, 6f, 72f);
        SaveSettings();
    }

    [RelayCommand]
    public void IncreasePlaybackSpeed() => StepPlaybackSpeed(+0.1f);
    [RelayCommand]
    public void DecreasePlaybackSpeed() => StepPlaybackSpeed(-0.1f);
    private void StepPlaybackSpeed(float delta)
    {
        var speed = MathF.Round((PlaybackSpeed + delta) * 10f) / 10f;
        PlaybackSpeed = Math.Clamp(speed, 0.1f, 4f);
    }


    private Task MessageErrorAsync(string messageKey) =>
    _message.ShowAsync(LocalizationService.Default[LangKeys.Gui_Error], LocalizationService.Default[messageKey]);


    #region 快捷键

    // 插件动作的快捷键挂在独立 owner 下,插件重载时整批换掉,不影响内置快捷键。
    private readonly object _pluginShortcutOwner = new();

    /// <summary>
    /// 把 <see cref="ShortcutDefinitions"/> 里的内置快捷键注册进
    /// <see cref="InputManager"/>,由此获得与窗口/焦点无关的全局派发
    /// (主窗口、浮动窗口、焦点落在任意面板里都生效)。
    /// 撤销/重做是编辑器自身的局部快捷键,不在这里注册。
    /// </summary>
    private void RegisterShortcuts()
    {
        // 设计时/重建时可能重复进构造函数,先清掉本 vm 名下的旧注册。
        InputManager.Instance.Unregister(this);

        foreach (var def in ShortcutDefinitions.All)
        {
            // 带图标键的条目属于插件动作,由 RebuildPluginShortcuts 按实际插件注册。
            if (def.PluginIconKey is not null) continue;
            if (ResolveCommand(def.FunctionKey) is not { } command) continue;

            InputManager.Instance.Register(def.Gesture, command, owner: this);
        }

        RebuildPluginShortcuts();
    }

    /// <summary>快捷键键名 -> 命令。新增全局快捷键时在这里挂上命令即可。</summary>
    private ICommand? ResolveCommand(string functionKey) => functionKey switch
    {
        nameof(LangKeys.Gui_Save) => SaveFileCommand,

        nameof(LangKeys.Shortcut_PlayStop) => PlayStopCommand,
        nameof(LangKeys.Shortcut_PlayPause) => PlayPauseCommand,
        nameof(LangKeys.Shortcut_PlayIncludeOp) => PlayIncludeOpCommand,

        nameof(LangKeys.Shortcut_IncreaseFontSize) => IncreaseFontSizeCommand,
        nameof(LangKeys.Shortcut_DecreaseFontSize) => DecreaseFontSizeCommand,

        nameof(LangKeys.Shortcut_IncreasePlaybackSpeed) => IncreasePlaybackSpeedCommand,
        nameof(LangKeys.Shortcut_DecreasePlaybackSpeed) => DecreasePlaybackSpeedCommand,

        _ => null,
    };

    /// <summary>按当前插件列表重建插件动作快捷键(图标键 -> 快捷键)。</summary>
    private void RebuildPluginShortcuts()
    {
        InputManager.Instance.Unregister(_pluginShortcutOwner);

        foreach (var plugin in _pluginsMng.Plugins)
        {
            foreach (var action in plugin.Actions)
            {
                if (ShortcutDefinitions.ForPluginIcon(action.IconKey) is not { } def) continue;

                InputManager.Instance.Register(
                    def.Gesture,
                    ExecutePluginActionCommand,
                    action,
                    _pluginShortcutOwner);
            }
        }
    }

    #endregion

    [RelayCommand]
    public void CheckUpdateAsync()
    {

    }


    // 手动滑动时调用
    [RelayCommand]
    public async Task SeekPlayheadTime(double time)
    {
        PlayheadTime = time;
        MoveCaretToPlayhead(time);

        if (_viewState is ViewStatus.Playing)
        {
            await PauseAsync(true);
        }
    }

    #endregion



    #region Commands - 连接

    private async Task EnsurePlayableAsync()
    {
        if (!_pushedSetting)
        {
            // 播放器拒绝（或没回应）时不算推过，下次再试。
            _pushedSetting = await _player.OptionsAsync(Settings.ViewSetting, Settings.VolumeSetting);
        }
        if (_viewState is not (ViewStatus.Loaded or ViewStatus.Paused))
        {
            await PushResourcesToPlayerAsync();
        }
    }

    private async Task SynchronizeConnectedPlayersAsync()
    {
        await _syncGate.WaitAsync();
        try
        {
            // 没有 track 就没有可推的资源；路径拿不到不再是阻断条件，
            // FilesAsync 会用空串表达「该字段无法提供」。
            if (!_player.IsConnected || Maidata is null || _chartFolder?.Track is null)
                return;
            if (_viewState is ViewStatus.Playing or ViewStatus.Paused)
                return;

            await PushResourcesToPlayerAsync();
            await EnsurePlayableAsync();
            await ParseAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] player sync: {ex.Message}");
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task LoadAsync()
    {
        if (_chartFolder?.Track is null) return;
        if (!await _player.ConnectAsync()) return;
        await PushResourcesToPlayerAsync();
        if (_chartDirty) await ParseAsync();
    }
    private async Task ParseAsync()
    {
        if (Maidata is null) return;
        if (!_player.IsConnected || _viewState is ViewStatus.Playing)
        {
            _chartDirty = true;
            return;
        }
        _chartDirty = false;
        await EnsurePlayableAsync();
        await _player.SimaiAsync(
            Maidata.Fumens[SelectedDifficulty] ?? string.Empty,
            Maidata.Offset,
            Maidata.Title,
            Maidata.Artist,
            Maidata.Designer,
            Maidata.Levels[SelectedDifficulty] ?? string.Empty,
            SelectedDifficulty);
    }
    public async Task PlayAsync(PlaybackMode mode)
    {
        if (Maidata is null) return;
        if (!await _player.ConnectAsync()) return;

        // 谱面在断连/播放期间改过的话，这里补推一次解析。
        if (_chartDirty)
            await ParseAsync();
        _lastPlayTime = PlayheadTime;

        await EnsurePlayableAsync();
        if (await _player.PlayAsync(mode, PlayheadTime, PlaybackSpeed))
            EnterPlaying();
        else
            _viewState = ViewStatus.Error;
    }
    /// <summary>
    /// 暂停/停止播放，两者在线格式上是同一个 Pause：停下后播放头回退到本段起点，
    /// 除非 <paramref name="keepCurrentPlayhead"/>（拖动播放头后的停顿要停在原地）。
    /// </summary>
    [RelayCommand]
    public async Task PauseAsync(bool? keepCurrentPlayhead = false)
    {
        if (!await _player.ConnectAsync()) return;

        if (!await _player.PauseAsync())
        {
            _viewState = ViewStatus.Error;
            return;
        }

        EnterPaused();

        await Task.Delay(33); // 这里等一下Tick结束
        // 纯暂停保留本段起点，交给之后的停止键回退；停止则用完即清。
        if (keepCurrentPlayhead == true) return;
        if (_lastPlayTime.HasValue)
            PlayheadTime = _lastPlayTime.Value;
        _lastPlayTime = null;
    }


    /// <summary>Inspector 的播放/暂停切换键。</summary>
    [RelayCommand]
    public async Task PlayPause()
    {
        if (Maidata is null) return;
        if (!await _player.ConnectAsync()) return;

        switch (_viewState)
        {
            case ViewStatus.Playing:
                await PauseAsync(true);
                break;
            // 协议没有独立的 Resume：从当前播放头重新发一次 Play 即为继续播放。
            default:
                await PlayAsync(PlaybackMode.Normal);
                break;
        }
    }
    /// <summary>快捷键的播放/停止切换。</summary>
    [RelayCommand]
    public async Task PlayStop()
    {
        if (Maidata is null) return;
        if (!await _player.ConnectAsync()) return;

        switch (_viewState)
        {
            case ViewStatus.Playing:
                await PauseAsync(false);
                break;
            default:
                await PlayAsync(PlaybackMode.Normal);
                break;
        }
    }
    /// <summary>Inspector 的录制模式按键。</summary>
    [RelayCommand]
    public async Task PlayIncludeOp()
    {
        if (Maidata is null) return;
        if (!IsConnected) return;

        await PlayAsync(PlaybackMode.IncludeOp);
        PlayheadTime -= 5;
    }
    /// <summary>Menu 的导出视频按键。</summary>
    [RelayCommand]
    public async Task PlayRecord()
    {
        if (Maidata is null) return;
        if (!IsConnected) return;

        await PlayAsync(PlaybackMode.Record);
    }

    //TODO: IncludeOp五秒延迟

    //====================播放连接====================





    /// <summary>进入播放态：以当前播放头为基准，按当前倍速开始推进。</summary>
    private void EnterPlaying()
    {
        _viewState = ViewStatus.Playing;
        _timelineClock.Reset();
        _timelineOrigin = PlayheadTime;
        _timelineSpeed = PlaybackSpeed;
        _timelineClock.Restart();
    }

    /// <summary>进入暂停态：停住播放头。</summary>
    private void EnterPaused()
    {
        _viewState = ViewStatus.Paused;
        _timelineClock.Stop();
    }





    #endregion




    #region WIRE UP

    private bool _isSlidingPlayhead;
    partial void OnCaretOffsetChanged(int value)
    {
        if (_isSlidingPlayhead || Chart is null) return;
        var timings = Chart.Timings;
        var caretTime = 0d;
        var combo = 0;
        if (timings.Length == 0) return;

        foreach (var t in timings)
        {
            if ((long)t.FumenPos >= value)
            {
                caretTime = t.Time;
                break;
            }
            else
            {
                combo += 1;
            }
        }

        CaretTime = caretTime;
        CaretCombo = combo;

        if (InputManager.Instance.IsControl)
        {
            PlayheadTime = caretTime;
        }
    }
    partial void OnPlayheadTimeChanged(double value)
    {
        // Ordinary time updates move the caret only when following is enabled; explicit seeks do so directly.
        if (IsFollowCursor) MoveCaretToPlayhead(value);
    }

    private void MoveCaretToPlayhead(double time)
    {
        if (Chart is null) return;
        var timings = Chart.Timings;
        if (timings.Length == 0) return;

        int lo = 0, hi = timings.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (timings[mid].Time <= time)
                lo = mid + 1;
            else
                hi = mid;
        }

        var index = lo == 0 ? 0 : lo - 1;
        _isSlidingPlayhead = true;
        try
        {
            CaretTime = timings[index].Time;
            CaretCombo = index;
            CaretOffset = (int)timings[index].FumenPos - 1;
        }
        finally
        {
            _isSlidingPlayhead = false;
        }
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        _timelineTimer.Stop();

        // 全局快捷键按 vm 归属注销,避免重开窗口后重复注册。
        InputManager.Instance.Unregister(this);
        InputManager.Instance.Unregister(_pluginShortcutOwner);

        SaveChartEditRecord();
        SaveLayout();

        await _player.DisposeAsync();
        _chartEditMng.Dispose();
        _pluginsMng.Dispose();

        GC.SuppressFinalize(this);
    }
}
