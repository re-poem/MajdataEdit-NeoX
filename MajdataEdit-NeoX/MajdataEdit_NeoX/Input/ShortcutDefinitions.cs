using Avalonia.Input;
using MajdataEdit_NeoX.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace MajdataEdit_NeoX.Input;

internal static class ShortcutDefinitions
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("Gui_Save", "Ctrl + S", new(Key.S, KeyModifiers.Control)),

        new("Shortcut_FindAndReplace", "Ctrl + F", new(Key.F, KeyModifiers.Control)),
        new("Shortcut_Undo", "Ctrl + Z", new(Key.Z, KeyModifiers.Control)),
        new("Shortcut_Redo", "Ctrl + Y", new(Key.Y, KeyModifiers.Control)),
        new("Shortcut_Redo", "Ctrl + Shift + Z", new(Key.Z, KeyModifiers.Control | KeyModifiers.Shift)),

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
}
