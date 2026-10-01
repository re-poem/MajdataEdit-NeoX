using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Models.Fumen;
using MajWebSocket.Types;
using System;
using System.Diagnostics;

namespace MajdataEdit_NeoX.ViewModels;

public enum AboutPages { Discord, QQ, GitHub, GitHub_ViewX, MajNet }
public partial class MainViewModel : ViewModelBase
{
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
    public partial MaidataFile? Maidata { get; private set; } = MaidataFile.Parse("&title=红日\r\n&artist=李克勤\r\n&des=Sun Jun\r\n&first=0.02\r\n&des_5=Sun Jun\r\n&lv_5=13+\r\n&inote_5=(120){4},,,\r\n{32}B1,B2,B3,B4,B5,B6,B7,B8,\r\n\r\n{8}1bxp6[8:4]b,,,,6xqq3[4:2]*qq3[4:2],,,,\r\n{12}3xp8[4:2]b,,4x,,5x,,6bhx[32:1],,6hx[32:1],,6hx[32:1],,\r\n{8}6bxpp3[8:4]b,,,,3xpp8[4:2]b,,,,\r\n{8}8xqq5[4:2]b,1x,2x,3x,4x-1[8:1]*-1[8:1],4b,4b,,\r\n\r\n{4}7h[4:1]/E1/B8f/E8/A8,\r\n{32}6,5h[4:2],,,,,,,B4,,,,,,,,\r\n{32}2,1h[4:2],,,,,,,E3,,,,,,,,\r\n{32}4,3h[4:2],,,,,,,B2,,,,,,,,\r\n{32}8,7h[4:2],,,,,,,E1,,,,,,,,\r\n{32}2,1h[4:2],,,,,,,B8,,,,,,,,\r\n{32}6,5h[4:2],,,,,,,E7,,,,,,,,\r\n{32}1,2h[4:2],,,,,,,E3,,,,,,,,\r\n{32}4,3h[4:2],,,,,,,E5,,,,,,,,\r\n{4}B2/E3f/B3,46,\r\n{32}6,7,8h[4:2],,,,,,B1/B2,,,,,,,,\r\n{32}2,3,4h[32:6],,,,,,\r\n{16}1,3,1,2,8,1,7,8,\r\n\r\n{32}6-8[16:1],5,,,8,,,,6/3,,,,1,,,,\r\n{32}6/2-8[16:1],3,,,8,,,,2/5,,,,7,,,,\r\n{32}2/5-3[16:1],4,,,1,,,,5/2,,,,2,,,,\r\n{32}5/1-3[16:1],8,,,6,,,,1/5,,,,6,,,,\r\n{32}5/8-6[16:1],1,,,6,,,,8/3,,,,5,,,,\r\n{32}6/3-1[16:1],4,,,1,,,,3/6q8[4:1],,,,5,,,,{8}2,2,3,4,4-7[16:2],,\r\n\r\n{8}4/6>3[64:9],5,6<8>1[8:1],,6/2-4[8:1]*-5[8:1]<1[64:7],,2,,\r\n{8}4,3,2-4<5[8:1],,2/1-5[8:1],,1/8V64[64:11],,84,3,2<5[4:1],,5,4,3,2,1,8w4[8:2],8,,,,\r\n{16}3,7,3,,2b/8b,,,,\r\n\r\n{48}1b,28,3pp6[4:2]/7,,,,,,,,,,,,,,,,,,,,,,\r\n6b,7,8pp2<7[32:10],,,,,,,,,,,,,,,,,,,,,,\r\n{12}4b>1[8:1],,6,,7,,8b<5[8:1],,4,,3,,\r\n{12}2b<7[8:1],,4,,5,,6b>3[8:1],,2,,1,,\r\n{8}4w8[4:1]/8b,,,,,17,26,,\r\n\r\n{32}\r\n35,,,,2,,,,4/1,E2,,,7,,,,58,,,,1,,,,8/6,B6,,,7,,,,\r\n4-6[8:1]/6,,,,5,,,,2/4-2[8:1],,,,3,,,,4-8[8:1]/B5/B6,,,,,,,,47,,,,,,,,\r\n\r\n1/7-5[8:1],B7,,,2,,,,1/7-3[8:1],,,,2,,,,4/7-1[8:1],,,,2,,,,\r\n4>1[8:1]/7,,,,3,,,,4/5<7[8:1],,,,,,,,4p8[8:1]/5-8[8:1],,,,,,,,45,,,,,,,,\r\n\r\n{8}17,26,3q5[4:1]/5,,3/5<2[4:1],,A6/E6/B6,,\r\n6,7,8p7[4:1],,7<2[4:1]/8,,A7/E8/B7,,\r\n7,6,5>7[4:1],,7,6,5,4,3,2w6[4:1],2,,,,\r\n\r\n{16}4,7,3,8,2,,{48}1b,28,3pp6>3[4:3]/7,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\r\n{8},,7qq2[4:2],,7,,,,4,3,2,17,6,17,6,5h,4,3,2,1qq4[4:2],,,,,,,,\r\n{48}5b,64,7pp2[4:2]/3,,,,,,,,,,,,,,,,\r\n{8},,,,3pp6[4:2],,3,,,,8,7,6,53,2,53,2,\r\n{16}1h,,,8,,,7qq4[4:2],,,,,,,,4>7[8:3],,3,,,,5,,,,\r\n{8}4-2[8:1],5,6-8[8:1],1,5-1[8:1],7,8,6,\r\n{16}3,5,3,5,2,6,3,5,2,6,3,5,{32}3,2,1,8,7,6,5,4,\r\n\r\n{8}3/Cf/E6h[4:4]/E5h[4:4]f/B5h[4:4],,{32}2,1,8,7,,,,,6,7,8,1,,,,,3,2,1,8,,,,,\r\n{8}E3h[4:4]/E4h[4:4]f/B3h[4:4],,{32}6,7,8,1,,,,,2,1,8,7,,,,,5,6,7,8,,,,,\r\n{8}E8h[4:4]/E7h[4:4]f/B7h[4:4],,{32}5,4,3,2,,,,,1,2,3,4,,,,,6,5,4,3,,,,,\r\n{8}E1h[8:2]f/B1h[8:3]f/B8h[8:4]f,,{32}7,6,5,4,,,,,4,5,6,7,,,,,\r\n\r\n{32}1-5[8:1]b/8-4[8:1]b,27,36,45,,,,,\r\n{8}1b-3[8:1]b/8b<5[8:1]b,,1bqq7[8:1]b/8bpp2[8:1]b,,1b/8b,,\r\n\r\n{32}5-1[8:1]b/4-8[8:1]b,63,72,81,,,,,\r\n{8}5b-7[8:1]b/4b>1[8:1]b,,5bqq3[8:1]b/4bpp6[8:1]b,,5b/4b,,36,27,\r\n\r\n{32}1,8,7,A6h[32:29]/E6h[32:29],,,,,,,,,,,,,\r\n{32}8,1,2,A3h[32:13]f/E4h[32:13],,,,,,,,,,,,,\r\n{8}Ch[4:1]f/B2/E3/B7/E7,,,,\r\n{32}2,7,3,6,{16}4,5,1,8,3,6,\r\n\r\n{48}4b,35,2qq7[4:2]b/6,,,,,,,,,,,,,,,,,,,,,,\r\n6b,5,4v3<6[32:10]b,,,,,,,,,,,,,,,,,,,,,,\r\n8b,1,2q8>1[4:1]b,,,,,,,,,,,,,,,,,,,,,,\r\n6b,5,4q5[4:1]b,,,,,,,,,,,,,,,,,,,,,,\r\n1b,2,3q8>2[16:5]b,,,,,,,,,,,,,,,,,,,,,,,,,,,,\r\n5b,4,3<3[8:5]b,,,,,,,,,,,,,,,,,,,,,\r\n\r\n{48},,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\r\n\r\n{32}1b/8b,2b/7b,3b,4,5,6,7,8,1,2,3,4,5,6,7,8,1,2,3,4,5,6,7,8,1,2,3,4,5,6,7,8,1,2,3,4,5,6,7,8,1,2,3,4,5,6,7,8,1,2,3,4,5,\r\n{24}7,4,7,4,6,3,6,3,5,2,5,2,5,2,5,2,5,2,5,2,5,2,\r\n{16}6,2,4w8[8:1]*w8[8:1],,,,4b,,,,\r\n");
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





    [ObservableProperty] public partial int CaretTime { get; set; }
    [ObservableProperty] public partial int CaretCombo { get; set; }
    [ObservableProperty] public partial int PlayheadTime { get; set; }
    [ObservableProperty] public partial float PlaybackSpeed { get; set; }
    [ObservableProperty] public partial bool IsFollowCursor { get; set; }




    // ========连接态========
    [ObservableProperty] public partial bool IsConnected { get; set; } = true;





    [RelayCommand]
    public void NewFile()
    {

    }
    [RelayCommand]
    public void OpenFile()
    {

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
