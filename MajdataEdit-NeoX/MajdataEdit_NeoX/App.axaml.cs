using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.PropertyGrid.Services;
using Dock.Settings;
using MajdataEdit_NeoX.Base;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Services;
using MajdataEdit_NeoX.ViewModels;
using MajdataEdit_NeoX.Views;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
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
        MajEnv.InitializeUserFiles();
        LaunchViewIfNotRunning();

        LocalizationService.Default.AddExtraService(new MajLocalizationService());

        // 组合根：IFileDialogService 注入 MainViewModel。
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var fileDialog = new AvaloniaFileDialogService(() => desktop.MainWindow?.StorageProvider);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(fileDialog),
            };
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // 集中给所有 Window 挂 InputManager:desktop.Windows 运行时是
            // AvaloniaList<Window>(实现 INotifyCollectionChanged)。MainWindow
            // 经由 MainWindow setter 进入集合,浮动窗口 (HostWindow) 由 Dock
            // 12 内部 window.Show() 也会进入集合,统一在 OnDesktopWindowsChanged
            // 里 Attach,无需每个 Window 单独写 Attach。
            if (desktop.Windows is INotifyCollectionChanged ncc)
            {
                ncc.CollectionChanged += OnDesktopWindowsChanged;
            }
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

    private static void OnDesktopWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null) return;
        foreach (Avalonia.Controls.Window window in e.NewItems)
        {
            InputManager.Instance.Attach(window);
        }
    }

    public static void LaunchViewIfNotRunning()
    {
        var path = OperatingSystem.IsWindows()
            ? MajEnv.GetPath("MajdataView.exe")
            : MajEnv.GetPath("MajdataView");
        if (!File.Exists(path))
        {
            Debug.WriteLine($"[ViewLauncherService] {path} not found");
            return;
        }

        if (Process.GetProcessesByName("MajdataViewX").Length > 0)
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ViewLauncherService] Failed to launch MajdataViewX: {ex.Message}");
        }
    }
}