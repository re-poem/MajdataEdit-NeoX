using Avalonia;
using Avalonia.Controls;
using Avalonia.PropertyGrid.Controls;
using MajdataEdit_NeoX.Models.Setting;
using System.Windows.Input;

namespace MajdataEdit_NeoX.Views;

public partial class SettingsView : UserControl
{
    [GeneratedDirectProperty(DefaultBindingMode = Avalonia.Data.BindingMode.TwoWay)]
    public partial MajSetting? Settings { get; set; }

    [GeneratedDirectProperty(DefaultBindingMode = Avalonia.Data.BindingMode.TwoWay)]
    public partial ICommand? SettingsChanged { get; set; }

    public SettingsView()
    {
        InitializeComponent();
    }
    private void PropertyGrid_CommandExecuted(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e is not RoutedCommandExecutedEventArgs args) return;

        SettingsChanged?.Execute(args.Property.Name);
    }
}
