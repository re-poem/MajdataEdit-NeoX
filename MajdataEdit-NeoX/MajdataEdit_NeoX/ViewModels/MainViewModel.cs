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
using MajdataEdit_NeoX.Types;
using MajdataEdit_NeoX.Views;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MajWebSocket.Types;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;

    public MainViewModel(IFileDialogService fileDialog)
    {
        _fileDialog = fileDialog;
        Settings.SettingsChanged += OnSettingsChanged;

        Factory = new MainDockFactory(this);
        Layout = Factory.CreateLayout()!;
        Factory.InitLayout(Layout!);
    }

    public MainViewModel() : this(new AvaloniaFileDialogService(() => null))
    {
    }

    public IFactory Factory { get; } = null!;
    public IRootDock Layout { get; } = null!;

    public SettingsService Settings { get; } = new();

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(AutoCheckUpdatesOnStartup));
        OnPropertyChanged(nameof(WaveAnimated));
        OnPropertyChanged(nameof(WordWrap));
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(AutoFocusView));
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

    public bool AutoCheckUpdatesOnStartup
    {
        get => Settings.Current.EditSetting.AutoCheckUpdatesOnStartup;
        set => Settings.Set(s => s.EditSetting.AutoCheckUpdatesOnStartup = value);
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

    public bool AutoFocusView
    {
        get => Settings.Current.EditSetting.AutoFocusView;
        set => Settings.Set(s => s.EditSetting.AutoFocusView = value);
    }

    [ObservableProperty]
    private bool _isDockDragEnabled;

    private readonly Dictionary<IDockable, bool> _originalCanDragStates = new();
    private readonly List<INotifyCollectionChanged> _subscribedCollections = new();

    public void EnableDockDragMode()
    {
        if (IsDockDragEnabled) return;

        try
        {
            _originalCanDragStates.Clear();
            IsDockDragEnabled = true;
            SubscribeCollectionChanges(Layout.VisibleDockables);
            EnableDragForDockables(Layout.VisibleDockables);
        }
        catch
        {
            try
            {
                DisableDockDragMode();
            }
            catch
            {
            }
            throw;
        }
    }

    public void DisableDockDragMode()
    {
        if (!IsDockDragEnabled && _originalCanDragStates.Count == 0 && _subscribedCollections.Count == 0) return;

        try
        {
            foreach (var collection in _subscribedCollections)
            {
                collection.CollectionChanged -= OnDockablesCollectionChanged;
            }
            _subscribedCollections.Clear();

            foreach (var kvp in _originalCanDragStates)
            {
                kvp.Key.CanDrag = kvp.Value;
            }
        }
        finally
        {
            _originalCanDragStates.Clear();
            IsDockDragEnabled = false;
        }
    }

    private void SubscribeCollectionChanges(IList<IDockable>? dockables)
    {
        if (dockables == null) return;

        foreach (var dockable in dockables)
        {
            if (dockable is not IDock dock) continue;

            if (dock.VisibleDockables is INotifyCollectionChanged collection && !_subscribedCollections.Contains(collection))
            {
                collection.CollectionChanged += OnDockablesCollectionChanged;
                _subscribedCollections.Add(collection);
            }
            SubscribeCollectionChanges(dock.VisibleDockables);
        }
    }

    private void OnDockablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsDockDragEnabled || e.NewItems == null) return;

        foreach (var item in e.NewItems)
        {
            if (item is IDockable dockable)
            {
                SubscribeCollectionChanges(new[] { dockable });
                EnableDragForDockables(new[] { dockable });
            }
        }
    }

    private void EnableDragForDockables(IList<IDockable>? dockables)
    {
        if (dockables == null) return;

        foreach (var dockable in dockables)
        {
            if (!_originalCanDragStates.ContainsKey(dockable))
                _originalCanDragStates[dockable] = dockable.CanDrag;
            dockable.CanDrag = true;

            if (dockable is IDock dock)
            {
                EnableDragForDockables(dock.VisibleDockables);
            }
        }
    }

    // ========文件========
    private string? _maidataDir { get; set; }

    [ObservableProperty]
    // 文件变化刷新
    [NotifyPropertyChangedFor(nameof(Title))]
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

    [ObservableProperty]
    public partial TrackInfo? TrackInfo { get; private set; } = null;

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




    [ObservableProperty] public partial int CaretTime { get; set; }
    [ObservableProperty] public partial int PlayheadTime { get; set; }

    [ObservableProperty] public partial int CaretCombo { get; set; }
    [ObservableProperty] public partial float PlaybackSpeed { get; set; }
    [ObservableProperty] public partial bool IsFollowCursor { get; set; }


    [ObservableProperty] public partial float ZoomLevel { get; set; }


    // ========连接态========
    [ObservableProperty] public partial bool IsConnected { get; set; } = true;





    [RelayCommand]
    public async Task NewFileAsync()
    {
        var file = await _fileDialog.OpenFileAsync(Lang.Resources.Gui_Open, [new FileDialogFilter("Audio Files", ["track.mp3", "track.ogg"])]);
        if (file == null) return;
        var dir = Path.GetDirectoryName(file);
        if (dir == null) return;

        _maidataDir = dir;
        Maidata = MaidataFile.New();
        TrackInfo = TrackReader.ReadTrack(file);
    }
    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var file = await _fileDialog.OpenFileAsync(Lang.Resources.Gui_Open, [new FileDialogFilter("Majdata Files", ["maidata.txt"])]);
        if (file == null) return;
        var dir = Path.GetDirectoryName(file);
        if (dir == null) return;

        _maidataDir = dir;
        Maidata = MaidataFile.Parse(File.ReadAllText(file));
        TrackInfo = TrackReader.ReadTrackFromDir(dir);
    }
    [RelayCommand]
    public void SaveFile()
    {

    }





    [RelayCommand]
    public void OpenRecover()
    {

    }
    [RelayCommand]
    public void OpenChartInfo()
    {
        var documentDock = Factory.FindDockable(Layout, d => d.Id == "DocumentDock") as DocumentDock;
        if (documentDock == null) return;

        var existing = documentDock.VisibleDockables?.FirstOrDefault(d => d.Id == "ChartInfo");
        if (existing != null)
        {
            Factory.CloseDockable(existing);
            return;
        }

        var chartInfo = new Document
        {
            Id = "ChartInfo",
            Title = "Chart Info",
            CanClose = true,
            CanDrag = false
        };

        Factory.AddDockable(documentDock, chartInfo);
        Factory.SetActiveDockable(chartInfo);
        Factory.SetFocusedDockable(documentDock, chartInfo);
    }

    [RelayCommand]
    public void OpenSettings()
    {
        var documentDock = Factory.FindDockable(Layout, d => d.Id == "DocumentDock") as DocumentDock;
        if (documentDock == null) return;

        var existing = documentDock.VisibleDockables?.FirstOrDefault(d => d.Id == "Settings");
        if (existing != null)
        {
            Factory.CloseDockable(existing);
            return;
        }

        var settings = new Document
        {
            Id = "Settings",
            Title = "Settings",
            CanClose = true,
            CanDrag = false
        };

        Factory.AddDockable(documentDock, settings);
        Factory.SetActiveDockable(settings);
        Factory.SetFocusedDockable(documentDock, settings);
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


    private IDockable? FindDockableById(string id)
    {
        if (Layout?.VisibleDockables == null) return null;

        foreach (var dockable in Layout.VisibleDockables.OfType<IDock>())
        {
            var result = FindInDock(dockable, id);
            if (result != null) return result;
        }
        return null;
    }

    private IDockable? FindInDock(IDock dock, string id)
    {
        if (dock.VisibleDockables == null) return null;

        foreach (var dockable in dock.VisibleDockables)
        {
            if (dockable.Id == id) return dockable;
            if (dockable is IDock childDock)
            {
                var result = FindInDock(childDock, id);
                if (result != null) return result;
            }
        }
        return null;
    }

    private IDock? FindDockById(string id)
    {
        if (Layout?.VisibleDockables == null) return null;

        foreach (var dockable in Layout.VisibleDockables.OfType<IDock>())
        {
            if (dockable.Id == id) return dockable;
            var result = FindDockInDock(dockable, id);
            if (result != null) return result;
        }
        return null;
    }

    private IDock? FindDockInDock(IDock dock, string id)
    {
        if (dock.VisibleDockables == null) return null;

        foreach (var dockable in dock.VisibleDockables.OfType<IDock>())
        {
            if (dockable.Id == id) return dockable;
            var result = FindDockInDock(dockable, id);
            if (result != null) return result;
        }
        return null;
    }
}
