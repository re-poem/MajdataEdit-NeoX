using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

namespace MajdataEdit_NeoX.Input;

/// <summary>
/// 全局输入管理:修饰键状态维护 + 全局快捷键派发。
///
/// Attach 到任意 <see cref="Window"/> 后,该 Window 上的
/// <see cref="InputElement.KeyDownEvent"/>/<see cref="InputElement.KeyUpEvent"/>
/// 在 Tunnel 阶段(<see cref="InputElement.AddHandler{TEventArgs}"/> handledEventsToo: true)
/// 实时汇总进 <see cref="Modifiers"/>,无论焦点在主窗口还是浮动窗口,
/// 修饰键状态都保持一致。
///
/// 同一批 Tunnel 处理器里还会匹配 <see cref="Register"/> 注册的全局快捷键并执行命令,
/// 因此快捷键不依赖任何视图/窗口的 KeyBindings:主窗口、Dock 浮动窗口、
/// 焦点落在任意面板时都同样生效。
///
/// 设计要点:
/// - handledEventsToo: true 让已被控件标记为 Handled 的事件也参与汇总
///   (例如菜单项吃掉 Ctrl+S,但 Ctrl 修饰键状态仍需保持)
/// - 所有 attach 的 Window 都失活(应用整体失焦)时才清空修饰键,
///   避免浮动窗口接走焦点时主窗口触发 Deactivated 误清空
/// - 同一 Window 重复 attach 是幂等的
/// - 后注册的快捷键优先匹配,允许插件/后续模块覆盖内置绑定
/// - 长按不重复触发(同一 Key 只在其 KeyDown 首次到达时匹配一次)
/// </summary>
public sealed class InputManager
{
    public static InputManager Instance { get; } = new();

    private readonly HashSet<Key> _pressedKeys = new();
    private readonly HashSet<Window> _attachedWindows = new();
    private readonly List<ShortcutEntry> _shortcuts = new();

    private KeyModifiers _modifiers;

    /// <summary>
    /// 当前按下的键(修饰键之外的普通键也记录,用于抑制长按重复触发)。
    /// </summary>
    public IReadOnlyCollection<Key> PressedKeys => _pressedKeys;

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

    #region 快捷键注册

    /// <summary>
    /// 注册一条全局快捷键,返回的 <see cref="IDisposable"/> 用于单独注销。
    /// </summary>
    /// <param name="gesture">按键组合。</param>
    /// <param name="command">命中的命令。</param>
    /// <param name="parameter">传给命令的参数。</param>
    /// <param name="owner">归属对象,便于 <see cref="Unregister"/> 成批注销。</param>
    /// <param name="canInvoke">
    /// 额外开关:返回 false 时本次按键不拦截,继续交给控件自身处理
    /// (例如焦点在文本框里时让控件自己消费该组合键)。
    /// </param>
    public IDisposable Register(
        KeyGesture gesture,
        ICommand command,
        object? parameter = null,
        object? owner = null,
        Func<bool>? canInvoke = null)
    {
        if (gesture is null) throw new ArgumentNullException(nameof(gesture));
        if (command is null) throw new ArgumentNullException(nameof(command));

        var entry = new ShortcutEntry(gesture, command, parameter, owner, canInvoke);
        _shortcuts.Add(entry);
        return new Subscription(() => _shortcuts.Remove(entry));
    }

    /// <summary>
    /// 注销某个 owner 注册的全部快捷键(插件重载等场景)。
    /// </summary>
    public void Unregister(object owner)
    {
        if (owner is null) return;
        _shortcuts.RemoveAll(entry => ReferenceEquals(entry.Owner, owner));
    }

    /// <summary>清空所有快捷键注册。</summary>
    public void ClearShortcuts() => _shortcuts.Clear();

    private bool TryInvokeShortcut(KeyEventArgs e)
    {
        // 后注册的优先:插件/后续模块可以覆盖内置绑定。
        for (var i = _shortcuts.Count - 1; i >= 0; i--)
        {
            var entry = _shortcuts[i];
            if (!Matches(entry.Gesture, e)) continue;
            if (entry.CanInvoke is { } canInvoke && !canInvoke()) continue;
            if (!entry.Command.CanExecute(entry.Parameter)) continue;

            entry.Command.Execute(entry.Parameter);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 手势与按键事件是否一致。直接比较 Key/KeyModifiers(而不是用
    /// <see cref="KeyGesture"/> 的匹配辅助),保证浮动窗口里的事件参数
    /// (没有 KeyBinding 上下文)同样能命中。
    /// </summary>
    private static bool Matches(KeyGesture gesture, KeyEventArgs e)
        => gesture.Key == e.Key && gesture.KeyModifiers == e.KeyModifiers;

    private sealed record ShortcutEntry(
        KeyGesture Gesture,
        ICommand Command,
        object? Parameter,
        object? Owner,
        Func<bool>? CanInvoke);

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }

    #endregion

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
        if (IsModifier(e.Key))
        {
            if (_pressedKeys.Add(e.Key)) UpdateModifiers();
            return;
        }

        // 长按产生的重复 KeyDown 不再触发快捷键。
        if (!_pressedKeys.Add(e.Key)) return;

        if (TryInvokeShortcut(e)) e.Handled = true;
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
