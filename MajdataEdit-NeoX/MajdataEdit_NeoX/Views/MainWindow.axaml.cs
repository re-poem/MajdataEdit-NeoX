using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.PropertyGrid.Services;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.Models.Plugin;
using MajdataEdit_NeoX.Types;
using MajdataEdit_NeoX.ViewModels;
using PropertyGenerator.Avalonia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MajdataEdit_NeoX.Views;

public class PluginMenuItem : MenuItem
{
    protected override Type StyleKeyOverride => typeof(MenuItem);
}
public class PluginSeparator : Separator
{
    protected override Type StyleKeyOverride => typeof(Separator);
}

public partial class MainWindow : Window
{
    /// <summary>关闭已确认：第二次 <c>Closing</c>（自己调 <see cref="Window.Close"/>) 直接放行。</summary>
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowControls();
        PropertyChanged += OnWindowPropertyChanged;
        Closing += OnClosing;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RebuildPluginUi();
    }

    #region Plugins

    [GeneratedDirectProperty]
    public partial IReadOnlyList<LuaPlugin>? Plugins { get; set; }
    partial void OnPluginsPropertyChanged(IReadOnlyList<LuaPlugin>? newValue) => RebuildPluginUi();

    private void RebuildPluginUi()
    {
        // 先无条件清理
        for (var i = EditMenu.Items.Count - 1; i >= 0; i--)
            if (EditMenu.Items[i] is PluginMenuItem or PluginSeparator)
                EditMenu.Items.RemoveAt(i);

        if (Plugins is null || DataContext is not MainViewModel vm) return;

        // 快捷键本身由 MainViewModel 经 InputManager 全局注册,这里只负责菜单项与提示手势。
        foreach (var plugin in Plugins)
        {
            if (plugin.Actions.Count == 0) continue;
            EditMenu.Items.Add(new PluginSeparator());

            foreach (var action in plugin.Actions)
            {
                var shortcut = ShortcutDefinitions.ForPluginIcon(action.IconKey);

                var item = new PluginMenuItem
                {
                    Command = vm.ExecutePluginActionCommand,
                    CommandParameter = action,
                    InputGesture = shortcut?.Gesture
                };
                ApplyHeader(item, action);
                EditMenu.Items.Add(item);
            }
        }
    }

    private static void ApplyHeader(MenuItem item, PluginAction action)
    {
        if (action.NameKey is { Length: > 0 } key)
        {
            item.Bind(MenuItem.HeaderProperty,
                new Binding($"[{key}]")
                {
                    Source = LocalizationService.Default,
                    Mode = BindingMode.OneWay
                });
        }
        else
        {
            item.Header = action.Name;
        }
    }

    #endregion



    #region Window Controls

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

    #endregion


    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;

        // 先拦住这次关闭：确认流程是异步的，等走完再自己 Close()。
        e.Cancel = true;
        _closeConfirmed = true;

        try
        {
            if (DataContext is not null)
                await AskCloseViewsAsync();

            if (DataContext is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        }
        finally
        {
            // 收尾里出岔子也得让窗口关掉，否则窗口会卡在"永远关不掉"。
            Close();
        }
    }

    /// <summary>View 还开着就问一句要不要一起关；用户选否就留着。</summary>
    private static async Task AskCloseViewsAsync()
    {
        var views = Process.GetProcessesByName("MajdataViewX");
        if (views.Length == 0) return;

        var confirmed = await MessageBox.ConfirmAsync(
            LocalizationService.Default[LangKeys.Msg_AskCloseView],
            LocalizationService.Default[LangKeys.Gui_Info]);
        if (!confirmed) return;

        views.FirstOrDefault()?.Kill();
    }
}