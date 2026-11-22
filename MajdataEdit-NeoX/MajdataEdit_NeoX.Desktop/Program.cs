using System;
using System.Diagnostics;
using Avalonia;
using Velopack;

namespace MajdataEdit_NeoX.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack
        try
        {
            VelopackApp.Build().Run();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Velopack] hook: {ex.Message}");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
