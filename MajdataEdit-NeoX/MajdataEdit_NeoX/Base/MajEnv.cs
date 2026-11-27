using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Velopack.Locators;

namespace MajdataEdit_NeoX.Base;

public static partial class MajEnv
{
    /// <summary>
    /// 程序目录，就是 Velopack 的 <c>current</c>，每次更新会被整个替换，只能当只读用。
    /// </summary>
    public static string MajBase
    {
        get
        {
            if (OperatingSystem.IsAndroid())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            if (OperatingSystem.IsIOS())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }

    /// <summary>
    /// 可写数据目录：即 <c>current</c> 的上一级）。
    /// </summary>
    public static string MajData
    {
        get
        {
            if (OperatingSystem.IsAndroid())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            if (OperatingSystem.IsIOS())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            return VelopackRoot ?? MajBase;
        }
    }

    /// <summary>
    /// Velopack 布局的根目录；不在布局里（开发直跑、移动端）为 null。
    /// 懒求值并缓存：Program.Main 里 VelopackApp.Run() 已经设好 locator，此后不会再变。
    /// </summary>
    private static readonly Lazy<string?> VelopackRootLazy = new(() =>
    {
        try
        {
            return VelopackLocator.IsCurrentSet ? VelopackLocator.Current.RootAppDir : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MajEnv] locator: {ex.Message}");
            return null;
        }
    });

    private static string? VelopackRoot => VelopackRootLazy.Value;

    /// <summary>程序目录下的路径（资源、View、随包文件）。</summary>
    public static string GetBasePath(string relativePath) =>
        Path.Combine(MajBase, relativePath);

    /// <summary>可写数据目录下的路径。</summary>
    public static string GetDataPath(string relativePath) =>
        Path.Combine(MajData, relativePath);


    public const string PlayheadShmName = "majdata_playhead";

    public static string DefaultBackgroundImageFile => GetBasePath("xxlb.png");

    public static string CompletionFile => GetDataPath("completions.json");
    public static string SettingsFile => GetDataPath("Settings.json");
    public static string LayoutFile => GetDataPath("layout.json");
    public static string CrashFile => GetDataPath("crash.log");
    public static string DatabaseFile => GetDataPath("editor.db");
    public static string GrammarFile => GetDataPath("simai.tmLanguage.json");
    public static string LuaPluginsPath => GetDataPath("Plugins");

    /// <summary>
    /// ffmpeg 原生库位置（波形解码用，见 Models/Track/FFmpegAudio.cs）。
    /// 桌面端与 MajdataViewX 一起打包，直接引用随 View 分发的库（Plugins/x86_64）；
    /// 移动端和 Browser 上库是打进包体的，交给 dlopen 的搜索路径，返回空串。
    /// </summary>
    public static string FFmpegLibraryPath
    {
        get
        {
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsBrowser())
                return string.Empty;

            // 与 LaunchViewIfNotRunning 一致：桌面端 MajdataViewX 与编辑器同目录部署
            return OperatingSystem.IsMacOS()
                ? GetBasePath(Path.Combine("MajdataViewX.app", "Contents", "Plugins", "x86_64"))
                : GetBasePath(Path.Combine("MajdataViewX_Data", "Plugins", "x86_64"));
        }
    }

    /// <summary>
    /// 把包内默认文件铺进可写目录，只在目标不存在时动手，用户自己改过的内容不会被默认值盖掉。
    /// </summary>
    public static void InitializeUserFiles()
    {
        Directory.CreateDirectory(MajData);
        Directory.CreateDirectory(LuaPluginsPath);

        CopyDefaultFile("completions.json", CompletionFile);
        CopyDefaultFile("simai.tmLanguage.json", GrammarFile);
        CopyDefaultFile("xxlb.png", DefaultBackgroundImageFile);
        CopyDefaultFile("Plugins/Mirror.lua", Path.Combine(LuaPluginsPath, "Mirror.lua"));
        CopyDefaultFile("Plugins/Subdivide.lua", Path.Combine(LuaPluginsPath, "Subdivide.lua"));
    }

    private static void CopyDefaultFile(string packageName, string destination)
    {
        if (File.Exists(destination)) return;

        var legacyFile = GetBasePath(packageName.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(legacyFile))
        {
            File.Copy(legacyFile, destination);
            return;
        }

        var resourceName = $"MajdataEdit_NeoX.{packageName.Replace('/', '.')}";
        using var source = typeof(MajEnv).Assembly.GetManifestResourceStream(resourceName);
        if (source is null) return;

        using var target = File.Create(destination);
        source.CopyTo(target);
    }




    public static void ActivateProcessWindow(Process? process)
    {
        if (process == null || process.HasExited) return;

        if (OperatingSystem.IsWindows())
        {
            IntPtr hWnd = process.MainWindowHandle;
            if (hWnd != IntPtr.Zero)
            {
                // 9 = SW_RESTORE（如果被最小化，先还原）
                ShowWindow(hWnd, 9);
                SetForegroundWindow(hWnd);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // 滚
        }
        else if (OperatingSystem.IsMacOS())
        {
            string script = $"tell application \"{process.ProcessName}\" to activate";
            Process.Start("osascript", $"-e \"{script}\"");
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static readonly string MAJDATA_VERSION_STRING = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";
}
