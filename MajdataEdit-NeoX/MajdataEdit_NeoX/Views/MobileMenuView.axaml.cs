using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.PropertyGrid.Services;
using MajdataEdit_NeoX.Models.Plugin;
using MajdataEdit_NeoX.ViewModels;
using System;
using System.ComponentModel;

namespace MajdataEdit_NeoX.Views;

/// <summary>Touch-friendly navigation for single-view mobile platforms.</summary>
public partial class MobileMenuView : UserControl
{
    private MainViewModel? _viewModel;
    private bool _isOpen;

    public MobileMenuView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDataContextChanged(e);
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebuildPluginItems();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Plugins))
            RebuildPluginItems();
    }

    private void RebuildPluginItems()
    {
        PluginItems.Children.Clear();
        if (_viewModel is null) return;

        foreach (var plugin in _viewModel.Plugins)
        {
            if (plugin.Actions.Count == 0) continue;
            PluginItems.Children.Add(new Separator());
            foreach (var action in plugin.Actions)
            {
                var button = new Button
                {
                    Command = _viewModel.ExecutePluginActionCommand,
                    CommandParameter = action
                };
                button.Classes.Add("drawerItem");
                button.Click += CloseDrawer;
                if (action.NameKey is { Length: > 0 } key)
                {
                    button.Bind(ContentControl.ContentProperty, new Binding($"[{key}]")
                    {
                        Source = LocalizationService.Default,
                        Mode = BindingMode.OneWay
                    });
                }
                else
                    button.Content = action.Name;
                PluginItems.Children.Add(button);
            }
        }
    }

    private void ToggleDrawer(object? sender, RoutedEventArgs e) => SetDrawerOpen(!_isOpen);
    private void CloseDrawer(object? sender, RoutedEventArgs e) => SetDrawerOpen(false);
    private void CloseOnScrim(object? sender, PointerPressedEventArgs e) => SetDrawerOpen(false);

    private void SetDrawerOpen(bool open)
    {
        _isOpen = open;
        Scrim.IsVisible = open;
        Drawer.IsHitTestVisible = open;
        MenuButton.IsVisible = !open;
        // 偏移量跟随抽屉自身宽度，避免宽度调整后收起时露出边缘。
        if (Drawer.RenderTransform is TranslateTransform transform)
            transform.X = open ? 0 : -Drawer.Width;
    }
}
