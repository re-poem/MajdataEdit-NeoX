using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MajdataEdit_NeoX.ViewModels;

namespace MajdataEdit_NeoX.Views;

public partial class MainWindow : Window
{
    private readonly HashSet<Key> _pressedKeys = new();

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, OnGlobalKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (s, e) => ResetPressedKeys();
        DockDragPressInterceptor.Attach(this);

#if DEBUG
        // 视觉 QA 钩子:MAJ_DEBUG_FORCE_DRAG_MASK=1 启动时直接开启拖拽遮罩,
        // 用于自动化截图验证遮罩样式(真实交互仍走 Ctrl+Shift+Alt 键盘路径)
        if (Environment.GetEnvironmentVariable("MAJ_DEBUG_FORCE_DRAG_MASK") == "1")
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => MajdataEdit_NeoX.ViewModels.DockDragModeState.Instance.IsActive = true,
                Avalonia.Threading.DispatcherPriority.Background);
        }
#endif
    }

    private bool IsControl() => _pressedKeys.Contains(Key.LeftCtrl) || _pressedKeys.Contains(Key.RightCtrl);

    private bool IsShift() => _pressedKeys.Contains(Key.LeftShift) || _pressedKeys.Contains(Key.RightShift);

    private bool IsAlt() => _pressedKeys.Contains(Key.LeftAlt) || _pressedKeys.Contains(Key.RightAlt);

    private bool IsCtrlShiftAltPressed => IsControl() && IsShift() && IsAlt();

    private static bool IsModifier(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt;

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsModifier(e.Key))
        {
            _pressedKeys.Add(e.Key);
        }

        if (IsCtrlShiftAltPressed && DataContext is MainViewModel vm)
        {
            vm.EnableDockDragMode();
        }
    }

    private void OnGlobalKeyUp(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Remove(e.Key);

        if (!IsCtrlShiftAltPressed && DataContext is MainViewModel vm)
        {
            vm.DisableDockDragMode();
        }
    }

    private void ResetPressedKeys()
    {
        _pressedKeys.Clear();
        if (DataContext is MainViewModel vm)
        {
            vm.DisableDockDragMode();
        }
    }
}
