using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MajdataEdit_NeoX.Base;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = new MainViewModel() };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}