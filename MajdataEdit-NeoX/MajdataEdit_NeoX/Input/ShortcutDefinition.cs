using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;

namespace MajdataEdit_NeoX.Input;

/// <summary>
/// 一条快捷键的静态描述。<see cref="FunctionKey"/> 既是本地化键,也是
/// MainViewModel 里"键 -> 命令"映射表的键;带 <see cref="PluginIconKey"/> 的条目
/// 由插件动作按图标键认领(插件热重载后依然生效)。
/// 这里只是目录,真正的全局注册由 <see cref="InputManager"/> 完成。
/// </summary>
public sealed record ShortcutDefinition(
    string FunctionKey,
    string GestureText,
    KeyGesture Gesture,
    string? PluginIconKey = null);

internal static class ShortcutDefinitions
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Gui_Save", "Ctrl + S", new(Key.S, KeyModifiers.Control)),

        new("Shortcut_PlayStop", "Ctrl + Shift + C", new(Key.C, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_PlayPause", "Ctrl + Shift + X", new(Key.X, KeyModifiers.Control | KeyModifiers.Shift)),
        new("Shortcut_PlayIncludeOp", "Ctrl + Shift + A", new(Key.A, KeyModifiers.Control | KeyModifiers.Shift)),

        new("Shortcut_IncreaseFontSize", "Ctrl + +", new(Key.OemPlus, KeyModifiers.Control)),
        new("Shortcut_DecreaseFontSize", "Ctrl + -", new(Key.OemMinus, KeyModifiers.Control)),

        new("Shortcut_IncreasePlaybackSpeed", "Ctrl + P", new(Key.P, KeyModifiers.Control)),
        new("Shortcut_DecreasePlaybackSpeed", "Ctrl + O", new(Key.O, KeyModifiers.Control)),

        new("Gui_MirrorHorizontally", "Ctrl + J", new(Key.J, KeyModifiers.Control), "mirror_h"),
        new("Gui_MirrorVertically", "Ctrl + K", new(Key.K, KeyModifiers.Control), "mirror_v"),
        new("Gui_Mirror180", "Ctrl + L", new(Key.L, KeyModifiers.Control), "mirror_180"),
        new("Gui_Rotate45", "Ctrl + ;", new(Key.OemSemicolon, KeyModifiers.Control), "rotate_r"),
        new("Gui_RotateNeg45", "Ctrl + '", new(Key.OemQuotes, KeyModifiers.Control), "rotate_l"),
    ];

    /// <summary>按插件动作的图标键找快捷键(菜单显示/注册用)。</summary>
    public static ShortcutDefinition? ForPluginIcon(string? iconKey)
        => iconKey is null
            ? null
            : All.FirstOrDefault(s => s.PluginIconKey == iconKey);
}
