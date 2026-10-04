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
    }

    private bool IsControl() => _pressedKeys.Contains(Key.LeftCtrl) || _pressedKeys.Contains(Key.RightCtrl);

    private bool IsShift() => _pressedKeys.Contains(Key.LeftShift) || _pressedKeys.Contains(Key.RightShift);

    private bool IsAlt() => _pressedKeys.Contains(Key.LeftAlt) || _pressedKeys.Contains(Key.RightAlt);

    private bool IsCtrlShiftAltPressed => IsControl() && IsShift() && IsAlt();

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        _pressedKeys.Add(e.Key);

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
