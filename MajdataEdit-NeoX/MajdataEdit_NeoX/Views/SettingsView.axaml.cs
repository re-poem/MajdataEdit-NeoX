using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MajdataEdit_NeoX.Models.Setting;
using MajdataEdit_NeoX.ViewModels;
using PropertyGenerator.Avalonia;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace MajdataEdit_NeoX.Views;

public partial class SettingsView : UserControl
{
    [GeneratedDirectProperty(DefaultBindingMode = Avalonia.Data.BindingMode.TwoWay)]
    public partial MajSetting? Settings { get; set; }

    public SettingsView()
    {
        InitializeComponent();
    }
}
