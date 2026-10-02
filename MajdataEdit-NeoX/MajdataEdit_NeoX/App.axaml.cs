using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.ViewModels;
using MajdataEdit_NeoX.Views;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX;

public partial class App : Application
{
    public override void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => LogCrashed((Exception)e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (sender, e) => LogCrashed(e.Exception);

        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    static void LogCrashed(Exception ex)
    {
        File.WriteAllText(MajEnv.CrashFile, $"{ex.Message}\n{ex.StackTrace}\n\n" +
            $"inner:{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 组合根：IFileDialogService 注入 MainViewModel。
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var fileDialog = new AvaloniaFileDialogService(() => desktop.MainWindow?.StorageProvider);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(fileDialog),
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            // 其他生命周期里没有 MainWindow.StorageProvider，注入恒返回 null 的 lambda。
            // 调用方拿到 null 会安全地跳过对话框。
            var fileDialog = new AvaloniaFileDialogService(() => null);
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel(fileDialog) };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var fileDialog = new AvaloniaFileDialogService(() => null);
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel(fileDialog)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}