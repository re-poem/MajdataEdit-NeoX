
using Avalonia;
using Avalonia.Controls;
using MajdataEdit_NeoX.ViewModels;
using System;

namespace MajdataEdit_NeoX.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        MobileMenu.IsVisible = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        // 移动端 / 浏览器没有 MainWindow，启动提示挂在这里（桌面端的 MainWindow 也承载 MainView，
        // 靠 VM 里的 _crashPrompted 保证只弹一次）。
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        // 桌面端由 MainWindow.Opened 负责：那时窗口已经 Show 出来，弹窗才有稳定的宿主。
        // 这里只服务没有 MainWindow 的单视图平台（Android / iOS / Browser）。
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            return;

        if (DataContext is MainViewModel vm)
            await vm.NotifyInterruptedExitAsync();
    }
}
