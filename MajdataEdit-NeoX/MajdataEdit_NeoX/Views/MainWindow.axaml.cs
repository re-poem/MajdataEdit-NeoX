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
using MajdataEdit_NeoX.Models.Setting;
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
    /// <summary>未保存确认框还开着：此时再次点关闭只交给正在走的那一遍，不叠弹窗。</summary>
    private bool _closeAsking;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWindowControls();
        PropertyChanged += OnWindowPropertyChanged;
        Closing += OnClosing;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is not MainViewModel vm) return;

        RestoreWindowPlacement(vm.Settings.WindowSetting);

        // 上次没正常退出的提示先问，免得被更新提示抢在前面。
        await vm.NotifyInterruptedExitAsync();
        await vm.AutoCheckUpdateAsync();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        RebuildPluginUi();
    }

    #region Plugins Menu

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


    #region Window Placement

    /// <summary>把上次窗口摆回原位。Opened 时窗口已经 Show 出来，直接改属性即可。</summary>
    private void RestoreWindowPlacement(MajWindowSetting placement)
    {
        // 显示器被拔掉后存下来的坐标会落在所有屏幕之外，那种情况就留着 CenterScreen 的居中结果。
        if (placement.Width >= 1 && placement.Height >= 1)
        {
            var target = new PixelRect(placement.PosX, placement.PosY, (int)placement.Width, (int)placement.Height);
            var area = Screens.All.FirstOrDefault(s => s.WorkingArea.Intersects(target))?.WorkingArea;

            if (area is { } bounds)
            {
                // 换了小屏幕时先把尺寸收进工作区，否则窗口会撑到屏幕外。
                var width = Math.Min(placement.Width, bounds.Width);
                var height = Math.Min(placement.Height, bounds.Height);

                // 先按普通窗口定位：最大化时 Position 会被忽略，得摆好再进最大化。
                WindowStartupLocation = WindowStartupLocation.Manual;
                Width = width;
                Height = height;
                Position = new PixelPoint(
                    Math.Clamp(placement.PosX, bounds.X, bounds.Right - (int)width),
                    Math.Clamp(placement.PosY, bounds.Y, bounds.Bottom - (int)height));
            }
        }

        if (placement.Maximized)
            WindowState = WindowState.Maximized;
    }

    /// <summary>记下这次的位置；只在确认退出后调用，取消退出不该覆盖已存的值。</summary>
    private void SaveWindowPlacement(MainViewModel vm)
    {
        var placement = vm.Settings.WindowSetting;
        placement.Maximized = WindowState == WindowState.Maximized;

        // 最大化/最小化时 Position 与 Width/Height 是平台改写过的值，只记普通状态下的，
        // 免得把还原后的大小弄丢。
        if (WindowState != WindowState.Normal) return;

        placement.PosX = Position.X;
        placement.PosY = Position.Y;
        placement.Width = Width;
        placement.Height = Height;
    }

    #endregion


    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // 已放行，或确认框还开着（用户又点了一次关闭）：都交给已经在走的那一遍。
        if (_closeConfirmed || _closeAsking) return;

        // 先拦住这次关闭：确认流程是异步的，等走完再自己 Close()。
        e.Cancel = true;

        try
        {
            _closeAsking = true;

            // 还有没落盘的改动就先问一句；用户取消就留在窗口里，下次关闭重来一遍。
            if (DataContext is MainViewModel vm && !await vm.ConfirmLeaveChartAsync())
                return;

            _closeConfirmed = true;

            // 位置在收尾前落盘，后面 Dispose 掉 VM 就拿不到设置了。
            if (DataContext is MainViewModel closingVm)
            {
                SaveWindowPlacement(closingVm);
                closingVm.SaveSettings();
            }

            if (DataContext is not null)
                await AskCloseViewsAsync();

            if (DataContext is IAsyncDisposable disposable)
                await disposable.DisposeAsync();
        }
        finally
        {
            _closeAsking = false;

            // 确认过了就关；没确认（用户取消）就留在原地——这次 Closing 已被 e.Cancel 拦掉。
            // 收尾里出岔子也得让窗口关掉，否则窗口会卡在"永远关不掉"。
            if (_closeConfirmed)
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