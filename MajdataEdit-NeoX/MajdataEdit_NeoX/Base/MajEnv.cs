using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MajdataEdit_NeoX.Base;

public static partial class MajEnv
{

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
    public static string GetPath(string relativePath) => 
        Path.Combine(MajBase, relativePath);


    // TODO: require another way 
    // public static string MmfAudioTimePath => GetPath("majdata_time.dat");
    public static string SettingsFile => GetPath("Settings.json");
    public static string LayoutFile => GetPath("layout.json");
    public static string CrashFile => GetPath("crash.log");
    public static string DatabaseFile => GetPath("editor.db");

    public static string CompletionFile => GetPath("completions.json");
    public static string GrammarFile => GetPath("simai.tmLanguage.json");
    public static string DefaultBackgroundImageFile => GetPath("xxlb.png");
    public static string LuaPluginsPath => GetPath("Plugins");

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
                ? GetPath(Path.Combine("MajdataViewX.app", "Contents", "Plugins", "x86_64"))
                : GetPath(Path.Combine("MajdataViewX_Data", "Plugins", "x86_64"));
        }
    }

    /// <summary>
    /// Copies editable defaults out of the package into the writable app-data directory.
    /// Existing files are deliberately preserved so user changes survive upgrades.
    /// </summary>
    public static void InitializeUserFiles()
    {
        Directory.CreateDirectory(LuaPluginsPath);

        // CopyDefaultFile("completions.json", CompletionFile); // in Completions.cs, auto generate
        CopyDefaultFile("simai.tmLanguage.json", GrammarFile);
        CopyDefaultFile("xxlb.png", DefaultBackgroundImageFile);
        CopyDefaultFile("Plugins/Mirror.lua", Path.Combine(LuaPluginsPath, "Mirror.lua"));
        CopyDefaultFile("Plugins/Subdivide.lua", Path.Combine(LuaPluginsPath, "Subdivide.lua"));
    }

    private static void CopyDefaultFile(string packageName, string destination)
    {
        if (File.Exists(destination)) return;

        var legacyFile = GetPath(packageName.Replace('/', Path.DirectorySeparatorChar));
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
