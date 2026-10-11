using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Models.Connection;
using MajdataEdit_NeoX.Models.Setting;
using MajdataEdit_NeoX.Models.Track;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Views;
using MajdataEdit_NeoX.Lang;
using Avalonia.PropertyGrid.Services;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MajProtocol.Types.Enums;
using MajdataEdit_NeoX.Input;
using Avalonia.Media.Imaging;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel : ViewModelBase, IAsyncDisposable
{
    // Design
    public MainViewModel() : this(new AvaloniaFileDialogService(() => null)) { }

    private readonly IFileDialogService _fileDialog;
    public MainViewModel(IFileDialogService fileDialog)
    {
        _fileDialog = fileDialog;
        Instance = this;
        _player = new PlayerConnection(Settings.Current.EditSetting.PlayerIps);

        Factory = new MainDockFactory(this);
        Layout = Factory.CreateLayout()!;
        Factory.InitLayout(Layout!);

        Settings.SettingsChanged += OnSettingsChanged;

        _player.OnViewStateChanged += OnPlayerStateChanged;
        _player.OnConnectedChanged += connected =>
        {
            _pushedSetting = false;
            _chartDirty = true;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(ActiveConnectionCount));
            _ = SynchronizeConnectedPlayersAsync();
        };

        _timelineTimer.Tick += (_, _) =>
        {
            if (!_player.IsConnected) return;
            if (_viewState is not (ViewStatus.Playing or ViewStatus.Paused)) return;
            PlayheadTime = _timelineOrigin + _timelineClock.Elapsed.TotalSeconds * _timelineSpeed;
        };
        _timelineTimer.Start();
    }


    /// <summary>
    /// 应用唯一的视图模型实例。dock 内容模板的绑定经由它取数,
    /// 使浮动窗口中的面板与主窗口获得同一份数据(浮动窗口的
    /// DockControl DataContext 是浮动根布局,取不到 MainViewModel)。
    /// </summary>
    public static MainViewModel Instance { get; private set; } = null!;


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



    #region 设置

    public SettingsService Settings { get; } = new();
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        _ = _player.SetPlayersAsync(Settings.Current.EditSetting.PlayerIps);
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(WaveAnimated));
        OnPropertyChanged(nameof(WordWrap));
        OnPropertyChanged(nameof(BackgroundImagePath));
        LocalizationService.Default.SelectCulture(Language);
    }

    [RelayCommand] // PropertyGrid 直接修改 Current 时，通过此入口广播设置变更。
    public void RefreshSettings()
    {
        Settings.NotifyChanged();
    }


    public string Language
    {
        get => Settings.Current.EditSetting.Language;
        set => Settings.Set(s => s.EditSetting.Language = value);
    }

    public float FontSize
    {
        get => Settings.Current.EditSetting.FontSize;
        set => Settings.Set(s => s.EditSetting.FontSize = value);
    }

    public bool WaveAnimated
    {
        get => Settings.Current.EditSetting.WaveAnimated;
        set => Settings.Set(s => s.EditSetting.WaveAnimated = value);
    }

    public bool WordWrap
    {
        get => Settings.Current.EditSetting.WordWrap;
        set => Settings.Set(s => s.EditSetting.WordWrap = value);
    }

    public string BackgroundImagePath
    {
        get => Settings.Current.EditSetting.BackgroundImagePath;
        set => Settings.Set(s => s.EditSetting.BackgroundImagePath = value);
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

    /// <summary>
    /// 从maidata.txt所在目录推导出maidata、track、bg、pv等文件的路径。
    /// </summary>
    /// <returns>是否扫描到最基本的maidata和track文件</returns>
    private bool UpdatePath(string maidataDir)
    {
        _maidataDir = maidataDir;
        _maidataPath = null;
        _trackPath = null;
        _imagePath = null;
        _videoPath = null;
        // scan
        if (_maidataDir is not null)
        {
            _maidataPath = Path.Combine(_maidataDir, "maidata.txt");
            if (!File.Exists(_maidataPath)) return false;

            _trackPath = _maidataDir + "/track.mp3";
            if (!File.Exists(_trackPath)) _trackPath = _maidataDir + "/track.ogg";
            if (!File.Exists(_trackPath)) return false;

            _imagePath = _maidataDir + "/bg.jpg";
            if (!File.Exists(_imagePath)) _imagePath = _maidataDir + "/bg.png";
            if (!File.Exists(_imagePath)) _imagePath = "";

            _videoPath = _maidataDir + "/pv.mp4";
            if (!File.Exists(_videoPath)) _videoPath = _maidataDir + "/bg.mp4";
            if (!File.Exists(_videoPath)) _videoPath = "";

            return true;
        }
        return false;
    }
    private string? _maidataDir;
    private string? _maidataPath;
    private string? _trackPath;
    private string? _imagePath;
    private string? _videoPath;
    private void UpdateResources()
    {
        if (File.Exists(_trackPath)) TrackInfo = TrackReader.ReadTrack(_trackPath);
        if (File.Exists(_imagePath)) Cover = new Bitmap(_imagePath);
    }

    [ObservableProperty]
    public partial TrackInfo? TrackInfo { get; private set; }
    [ObservableProperty]
    public partial Bitmap? Cover { get; set; }

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
    private ViewStatus _viewState = ViewStatus.Idle;
    private TaskCompletionSource? _busyTcs; // 等待非Busy状态

    private bool _chartDirty; // 谱面解析被跳过（断连/播放中），待连接且空闲后补推
    private bool _pushedSetting; // 在任一连接完成之后，有至少一次settings提交
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private double? _lastPlayTime; // 供Stop进行回退

    // 播放期间的时间线跟踪：本地 Stopwatch 推算（origin + elapsed × speed），
    private readonly Stopwatch _timelineClock = new();
    private double _timelineOrigin;
    private float _timelineSpeed = 1f;
    // 常驻 UI 时钟：非播放状态的 tick 直接跳过，因此不需要启停控制和跨线程取消那套机器。
    private readonly DispatcherTimer _timelineTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };


    #region Commands - 窗口

    [RelayCommand]
    public async Task NewFileAsync()
    {
        var file = await _fileDialog.OpenFileAsync(LocalizationService.Default[LangKeys.Gui_Open], [new FileDialogFilter("Audio Files", ["track.mp3", "track.ogg"])]);
        if (file == null) return;
        var dir = Path.GetDirectoryName(file);
        if (dir == null) return;

        File.Create(dir + "/maidata.txt");
        // TODO: 这里应该弹窗提示
        if (!UpdatePath(dir)) return;
        Maidata = MaidataFile.New();

        UpdateResources();
        await LoadAsync();
    }
    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var file = await _fileDialog.OpenFileAsync(LocalizationService.Default[LangKeys.Gui_Open], [new FileDialogFilter("Majdata Files", ["maidata.txt"])]);
        if (file == null) return;
        var dir = Path.GetDirectoryName(file);
        if (dir == null) return;

        // TODO: 这里应该弹窗提示，maidata.txt所在目录没有track文件，无法打开。
        if (!UpdatePath(dir)) return;
        Maidata = MaidataFile.Parse(File.ReadAllText(file));
        UpdateResources();

        await LoadAsync();
    }
    [RelayCommand]
    public async Task SaveFileAsync()
    {
        if (!File.Exists(_maidataPath)) return;
        if (Maidata is null) return;

        var text = MaidataFile.Deparse(Maidata);
        await File.WriteAllTextAsync(_maidataPath, text);
    }
    [RelayCommand]
    public async Task ReloadFileAsync()
    {
        if (!Directory.Exists(_maidataDir)) return;

        if (!UpdatePath(_maidataDir)) return;
        Maidata = MaidataFile.Parse(_maidataPath!);
        UpdateResources();
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
    public void ShowFindReplace()
    {

    }

    [RelayCommand]
    public void CheckUpdateAsync()
    {

    }

    #endregion



    #region Commands - 连接

    public async Task WaitNotBusyAsync()
    {
        if (_viewState != ViewStatus.Busy)
            return;

        _busyTcs ??= new();
        await _busyTcs.Task;
    }
    private async Task EnsureSettingAsync()
    {
        if (_pushedSetting) return;
        await _player.SettingAsync(Settings.Current.ViewSetting, Settings.Current.VolumeSetting);
        _pushedSetting = true;
    }

    private async Task SynchronizeConnectedPlayersAsync()
    {
        await _syncGate.WaitAsync();
        try
        {
            if (!_player.IsConnected || Maidata is null || _trackPath is null)
                return;
            if (_viewState is ViewStatus.Playing or ViewStatus.Paused)
                return;

            await _player.LoadAsync(_trackPath, _imagePath ?? string.Empty, _videoPath);
            await EnsureSettingAsync();
            await ParseAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] player sync: {ex.Message}");
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task LoadAsync()
    {
        if (_trackPath is null) return;
        if (!await _player.ConnectAsync()) return;
        await _player.LoadAsync(_trackPath, _imagePath ?? string.Empty, _videoPath);
        if (_chartDirty) await ParseAsync();
    }
    private async Task ParseAsync()
    {
        if (Maidata is null) return;
        if (!_player.IsConnected || _viewState is ViewStatus.Playing or ViewStatus.Paused)
        {
            _chartDirty = true;
            return;
        }
        _chartDirty = false;
        await EnsureSettingAsync();
        await _player.ParseAsync(
            Maidata.Fumens[SelectedDifficulty] ?? string.Empty,
            ExtractClockCount(Maidata),
            Maidata.Offset,
            Maidata.Title,
            Maidata.Artist,
            Maidata.Levels[SelectedDifficulty] ?? string.Empty,
            Maidata.Designer,
            SelectedDifficulty);
    }
    public async Task PlayAsync(PlaybackMode mode)
    {
        if (Maidata is null || _maidataDir is null) return;
        if (!await _player.ConnectAsync()) return;

        // 谱面在断连/播放期间改过的话，这里补推一次解析。
        if (_chartDirty)
            await ParseAsync();
        _lastPlayTime = PlayheadTime;

        await EnsureSettingAsync();
        await _player.PlayAsync(mode, PlayheadTime, PlaybackSpeed, _maidataDir);
    }
    public async Task PauseAsync()
    {
        // 能Pause了这些检查都可以撇了
        //if (Maidata is null || _maidataDir is null) return;
        //if (!await _player.ConnectAsync()) return;

        await _player.PauseAsync();
    }
    public async Task ResumeAsync()
    {
        // 能Resume了这些检查都可以撇了
        //if (Maidata is null || _maidataDir is null) return;
        //if (!await _player.ConnectAsync()) return;

        _lastPlayTime = PlayheadTime;

        await _player.ResumeAsync();
    }
    /// <summary>Inspector 的停止键。</summary>
    [RelayCommand]
    public async Task StopAsync()
    {
        if (!await _player.ConnectAsync()) return;
        await _player.StopAsync();

        await Task.Delay(33); // 这里等一下Tick结束
        if (_lastPlayTime.HasValue)
        {
            PlayheadTime = _lastPlayTime.Value;
        }
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
                await PauseAsync();
                break;
            case ViewStatus.Paused:
                await ResumeAsync();
                break;
            case ViewStatus.Busy:
                await WaitNotBusyAsync();
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

        switch (_viewState)
        {
            case ViewStatus.Busy:
                await WaitNotBusyAsync();
                break;
            default:
                await PlayAsync(PlaybackMode.IncludeOp);
                break;
        }
    }
    /// <summary>Menu 的导出视频按键。</summary>
    [RelayCommand]
    public async Task PlayRecord()
    {
        if (Maidata is null) return;
        if (!IsConnected) return;

        switch (_viewState)
        {
            case ViewStatus.Busy:
                await WaitNotBusyAsync();
                break;
            default:
                await PlayAsync(PlaybackMode.Record);
                break;
        }
    }

    //TODO: IncludeOp五秒延迟

    //====================播放连接====================





    private void OnPlayerStateChanged(ViewStatus oldState, ViewStatus newState)
    {
        _viewState = newState;
        if (newState == ViewStatus.Playing && oldState != ViewStatus.Playing)
        {
            // 对齐基准：进入 Playing 前（PlayStarted/PlayResumed）的事件已把镜像刷成本段起点。
            _timelineClock.Reset();
            _timelineOrigin = PlayheadTime;
            _timelineSpeed = PlaybackSpeed;
            _timelineClock.Restart();
        }
        if (newState == ViewStatus.Paused && oldState != ViewStatus.Paused)
        {
            _timelineClock.Stop();
        }
        // 让当前卡在等Busy的发送
        if (newState != ViewStatus.Busy)
        {
            _busyTcs?.TrySetResult();
            _busyTcs = null;
        }
    }

    private static int ExtractClockCount(MaidataFile file)
    {
        foreach (var cmd in file.Commands)
        {
            if (cmd.Key == "clock_count" && int.TryParse(cmd.Value, out var n))
                return n;
        }
        return 0;
    }



    #endregion




    #region WIRE UP

    private bool _isSlidingPlayhead;
    partial void OnCaretOffsetChanged(int value)
    {
        if (Chart is null) return;
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
                foreach (var note in t.Notes)
                    combo += 1;
            }
        }

        CaretTime = caretTime;
        CaretCombo = combo;

        if (InputManager.Instance.IsControl && !_isSlidingPlayhead)
        {
            PlayheadTime = caretTime;
        }
    }
    partial void OnPlayheadTimeChanged(double value)
    {
        if (Chart is null) return;
        // 播放时间线写入 PlayheadTime 只推进时钟/播放头；跟随开启时才带动编辑器 caret。
        if (!IsFollowCursor) return;
        _isSlidingPlayhead = true;

        var timings = Chart.Timings;
        if (timings.Length == 0) return;

        int lo = 0, hi = timings.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);

            if (timings[mid].Time < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        if (lo == 0)
            CaretOffset = (int)timings[0].FumenPos;
        else
        {
            if (lo == timings.Length)
                lo = timings.Length - 1;
            var a = timings[lo - 1].Time;
            var b = timings[lo].Time;
            lo = value - a <= b - value
                ? lo - 1
                : lo;

            CaretOffset = (int)timings[lo].FumenPos;
        }

        _isSlidingPlayhead = false;
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        _timelineTimer.Stop();
        Settings.SettingsChanged -= OnSettingsChanged;
        await _player.DisposeAsync();
    }
}
