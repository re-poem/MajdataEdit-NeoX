using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Models.Setting;
using MajdataEdit_NeoX.Models.Track;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.Views;
using MajdataEdit_NeoX.Lang;
using Avalonia.PropertyGrid.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MajWebSocket.Types;
using MajdataEdit_NeoX.Input;
using Avalonia.Media.Imaging;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel : ViewModelBase
{
    // Design
    public MainViewModel() : this(new AvaloniaFileDialogService(() => null)) { }

    private readonly IFileDialogService _fileDialog;
    public MainViewModel(IFileDialogService fileDialog)
    {
        _fileDialog = fileDialog;
        Instance = this;

        Settings.SettingsChanged += OnSettingsChanged;

        Factory = new MainDockFactory(this);
        Layout = Factory.CreateLayout()!;
        Factory.InitLayout(Layout!);
    }


    /// <summary>
    /// 应用唯一的视图模型实例。dock 内容模板的绑定经由它取数,
    /// 使浮动窗口中的面板与主窗口获得同一份数据(浮动窗口的
    /// DockControl DataContext 是浮动根布局,取不到 MainViewModel)。
    /// </summary>
    public static MainViewModel Instance { get; private set; } = null!;


    public IFactory Factory { get; } = null!;
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



    // ========设置========

    public SettingsService Settings { get; } = new();
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(WaveAnimated));
        OnPropertyChanged(nameof(WordWrap));
        OnPropertyChanged(nameof(BackgroundImagePath));
        RefreshSettings();
    }

    [RelayCommand] // 两条路径：SettingsGrid 的 CommandExecuted 事件，或手动 Settings.Set() 的事件
    public void RefreshSettings()
    {
        LocalizationService.Default.SelectCulture(Language);
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

    // ========文件========
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
    }

    // ========编辑态========
    [ObservableProperty]
    // 切换难度刷新
    [NotifyPropertyChangedFor(nameof(Level))]
    [NotifyPropertyChangedFor(nameof(Designer))]
    [NotifyPropertyChangedFor(nameof(Fumen))]
    [NotifyPropertyChangedFor(nameof(Chart))]
    public partial int SelectedDifficulty { get; set; }

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



    // ========临时态========
    // TODO: 没有效果
    public string WindowTitle
    {
        get
        {
            if (Maidata is null)
                return "MajdataEdit-NeoX";
            else
            {
                if (Maidata.IsDirty)
                    return $"MajdataEdit-NeoX - {Maidata.Title}*";
                else
                    return $"MajdataEdit-NeoX - {Maidata.Title}";
            }
        }
    }


    [ObservableProperty] public partial int CaretOffset { get; set; }
    [ObservableProperty] public partial double CaretTime { get; set; }
    [ObservableProperty] public partial int CaretCombo { get; set; }
    [ObservableProperty] public partial double PlayheadTime { get; set; }

    [ObservableProperty] public partial float PlaybackSpeed { get; set; }
    [ObservableProperty] public partial bool IsFollowCursor { get; set; }


    [ObservableProperty] public partial float ZoomLevel { get; set; } = 1f;


    // ========连接态========
    [ObservableProperty] public partial bool IsConnected { get; set; } = true;





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
    public void ReloadFile()
    {
        if (!Directory.Exists(_maidataDir)) return;

        if (!UpdatePath(_maidataDir)) return;
        Maidata = MaidataFile.Parse(_maidataPath!);
        UpdateResources();
    }




    [RelayCommand]
    public void OpenRecover()
    {

    }
    [RelayCommand]
    public void OpenChartInfo()
    {
        ToggleFloatDocument("ChartInfo", "Chart Info");
    }

    [RelayCommand]
    public void OpenSettings()
    {
        ToggleFloatDocument("Settings", "Settings");
    }

    /// <summary>
    /// 辅助面板(设置/Chart Info)以浮动窗口开关:主窗口 dock 单占无 tab,
    /// 不允许两个面板堆在同一 dock,因此这些面板从不进入主布局。
    /// </summary>
    private void ToggleFloatDocument(string id, string title)
    {
        var existing = FindDocumentAnywhere(id);
        if (existing is not null)
        {
            Factory.CloseDockable(existing);
            return;
        }

        var documentDock = Factory.FindDockable(Layout, d => d.Id == "DocumentDock") as DocumentDock;
        if (documentDock is null) return;

        var document = new Document
        {
            Id = id,
            Title = title,
            CanClose = true,
            CanDrag = true,
            DockCapabilityOverrides = new DockCapabilityOverrides()
        };
        if (Factory is MainDockFactory mainFactory)
        {
            mainFactory.AddFloat(documentDock, document);
        }
    }
    private IDockable? FindDocumentAnywhere(string id)
    {
        var inMain = Factory.FindDockable(Layout, d => d.Id == id);
        if (inMain is not null) return inMain;

        foreach (var host in Factory.HostWindows)
        {
            var layout = host.Window?.Layout;
            if (layout is null) continue;
            var found = Factory.FindDockable(layout, d => d.Id == id);
            if (found is not null) return found;
        }

        return null;
    }
    [RelayCommand]
    public void OpenShortcut()
    {

    }
    [RelayCommand]
    public void OpenBpmTap()
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
    public void Play(PlaybackMode mode)
    {

    }





    [RelayCommand]
    public void CheckUpdateAsync()
    {

    }









    // WIRE UP

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


}
