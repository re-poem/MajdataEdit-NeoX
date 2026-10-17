using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.ViewModels;

namespace MajdataEdit_NeoX.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowControls();
        PropertyChanged += OnWindowPropertyChanged;
        Closing += OnClosing;
    }
    public static readonly DirectProperty<MainWindow, bool> IsMaximizedProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, bool>(
            nameof(IsMaximized),
            o => o.IsMaximized);
    public bool IsMaximized => WindowState == WindowState.Maximized;

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            var oldValue = (WindowState)e.OldValue! == WindowState.Maximized;
            var newValue = (WindowState)e.NewValue! == WindowState.Maximized;

            if (oldValue != newValue)
                RaisePropertyChanged(IsMaximizedProperty, oldValue, newValue);
        }
    }

    private void ConfigureWindowControls()
    {
        if (OperatingSystem.IsMacOS())
            DesktopControls.IsVisible = false;
        else
            MacControls.IsVisible = false;
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }
}