using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.ViewModels;
using System;
using System.Diagnostics;

namespace MajdataEdit_NeoX.Views;

public enum AboutLinks { Discord, QQ, GitHub, MajNet }
public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();

        var version = typeof(AboutView).Assembly.GetName().Version;
        if (version is not null)
            VersionText.Text = version.ToString(3);
    }

    [RelayCommand]
    public void OpenAboutLink(AboutLinks link)
    {
        switch (link)
        {
            case AboutLinks.Discord: OpenBrowser("https://discord.gg/AcWgZN7j6K"); break;
            case AboutLinks.QQ: OpenBrowser("https://qm.qq.com/q/GAxbFZHP6A"); break;
            case AboutLinks.GitHub: OpenBrowser("https://github.com/re-poem/MajdataViewX"); break;
            case AboutLinks.MajNet: OpenBrowser("https://majdata.net/"); break;
        }
        static void OpenBrowser(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
