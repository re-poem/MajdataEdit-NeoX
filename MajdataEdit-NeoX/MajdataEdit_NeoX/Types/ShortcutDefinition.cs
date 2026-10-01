using Avalonia.Input;
using System.Collections.Generic;

namespace MajdataEdit_NeoX.Types;

public sealed record ShortcutDefinition(
    string FunctionKey,
    string GestureText,
    KeyGesture Gesture,
    string? PluginIconKey = null);
