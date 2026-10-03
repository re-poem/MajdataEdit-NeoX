using Cimai;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Models.Fumen;
using MajdataEdit_NeoX.Models.Track;
using MajdataEdit_NeoX.Services;
using MajWebSocket.Types;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel
    (IFileDialogService fileDialog) : ViewModelBase
{
    public MainViewModel() : this(new AvaloniaFileDialogService(() => null)) { }


    // ========文件========
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
        var file = await fileDialog.OpenFileAsync(Lang.Resources.Gui_Open, [new FileDialogFilter("Audio Files", ["track.mp3", "track.ogg"])]);
        if (file == null) return;

        Maidata = MaidataFile.New();
        TrackInfo = TrackReader.ReadTrack(file);
    }
    [RelayCommand]
    public async Task OpenFileAsync()
    {
        var file = await fileDialog.OpenFileAsync(Lang.Resources.Gui_Open, [new FileDialogFilter("Majdata Files", ["maidata.txt"])]);
        if (file == null) return;
        var dir = Path.GetDirectoryName(file);
        if (dir == null) return;

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

    }
    [RelayCommand]
    public void OpenSettings()
    {

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
    public void ShowFindReplace(AboutPages page)
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
}
