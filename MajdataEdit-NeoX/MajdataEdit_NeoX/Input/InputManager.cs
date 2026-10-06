using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace MajdataEdit_NeoX.Input;

/// <summary>
/// 全局按键修饰键状态维护。Attach 到任意 <see cref="Window"/> 后,
/// 该 Window 上的 <see cref="InputElement.KeyDownEvent"/>/<see cref="InputElement.KeyUpEvent"/>
/// 在 Tunnel 阶段(<see cref="InputElement.AddHandler{TEventArgs}"/> handledEventsToo: true)
/// 实时汇总进 <see cref="Modifiers"/>,无论焦点在主窗口还是浮动窗口,
/// 修饰键状态都保持一致。
///
/// 设计要点:
/// - handledEventsToo: true 让已被控件标记为 Handled 的事件也参与汇总
///   (例如菜单项吃掉 Ctrl+S,但 Ctrl 修饰键状态仍需保持)
/// - 所有 attach 的 Window 都失活(应用整体失焦)时才清空修饰键,
///   避免浮动窗口接走焦点时主窗口触发 Deactivated 误清空
/// - 同一 Window 重复 attach 是幂等的
/// </summary>
public sealed class InputManager
{
    public static InputManager Instance { get; } = new();

    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<Window> _attachedWindows = new();

    private KeyModifiers _modifiers;

    /// <summary>
    /// 当前按下的修饰键集合。修饰键(LeftCtrl/RightCtrl/LeftShift/...)
    /// 之外的普通键不参与汇总。
    /// </summary>
    public KeyModifiers Modifiers => _modifiers;

    /// <summary>
    /// 修饰键状态变化时触发,接收方:第二个事件参数为变化后的 <see cref="KeyModifiers"/>。
    /// 始终在 UI 线程触发。
    /// </summary>
    public event EventHandler<KeyModifiers>? ModifiersChanged;

    public bool IsControl => HasFlag(KeyModifiers.Control);
    public bool IsShift => HasFlag(KeyModifiers.Shift);
    public bool IsAlt => HasFlag(KeyModifiers.Alt);
    public bool IsMeta => HasFlag(KeyModifiers.Meta);

    private InputManager() { }

    /// <summary>
    /// Attach 到指定 <see cref="Window"/>。多次 attach 同一 Window 是幂等的,
    /// 仅注册一次事件处理。
    /// </summary>
    public void Attach(Window window)
    {
        if (window is null) return;
        if (!_attachedWindows.Add(window)) return;

        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        window.Deactivated += OnWindowDeactivated;
        window.Closed += OnWindowClosed;
    }

    /// <summary>
    /// 显式取消 attach(主要给测试或窗口重建场景)。Closed 时也会自动清理。
    /// </summary>
    public void Detach(Window window)
    {
        if (window is null) return;
        if (!_attachedWindows.Remove(window)) return;

        window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        window.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        window.Deactivated -= OnWindowDeactivated;
        window.Closed -= OnWindowClosed;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsModifier(e.Key)) return;
        if (!_pressedKeys.Add(e.Key)) return;
        UpdateModifiers();
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (!_pressedKeys.Remove(e.Key)) return;
        UpdateModifiers();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // 仅当所有 attach 的 Window 都已失活(应用整体失焦)时才清空,
        // 否则浮动窗口接走焦点会触发主窗口 Deactivated,误清修饰键状态。
        foreach (var w in _attachedWindows)
        {
            if (ReferenceEquals(w, sender)) continue;
            if (w.IsActive) return;
        }

        if (_pressedKeys.Count == 0) return;
        _pressedKeys.Clear();
        UpdateModifiers();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            _attachedWindows.Remove(window);
        }
    }

    private void UpdateModifiers()
    {
        var mods = KeyModifiers.None;
        if (HasKey(Key.LeftCtrl) || HasKey(Key.RightCtrl)) mods |= KeyModifiers.Control;
        if (HasKey(Key.LeftShift) || HasKey(Key.RightShift)) mods |= KeyModifiers.Shift;
        if (HasKey(Key.LeftAlt) || HasKey(Key.RightAlt)) mods |= KeyModifiers.Alt;
        if (HasKey(Key.LWin) || HasKey(Key.RWin)) mods |= KeyModifiers.Meta;

        if (mods == _modifiers) return;
        _modifiers = mods;
        ModifiersChanged?.Invoke(this, mods);
    }

    private bool HasFlag(KeyModifiers flag) => (_modifiers & flag) == flag;
    private bool HasKey(Key key) => _pressedKeys.Contains(key);

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
          or Key.LeftShift or Key.RightShift
          or Key.LeftAlt or Key.RightAlt
          or Key.LWin or Key.RWin;
}