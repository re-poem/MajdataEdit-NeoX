using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Models.Setting;
using MajdataEdit_NeoX.ViewModels;
using PropertyGenerator.Avalonia;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
        SettingsChanged?.Execute(null);
    }
}
