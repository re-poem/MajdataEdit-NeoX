using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using MajdataEdit_NeoX.Input;
using MajdataEdit_NeoX.ViewModels;

namespace MajdataEdit_NeoX.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }
}