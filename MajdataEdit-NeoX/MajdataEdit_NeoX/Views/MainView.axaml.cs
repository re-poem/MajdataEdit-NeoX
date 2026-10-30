
using Avalonia;
using Avalonia.Controls;
using System;

namespace MajdataEdit_NeoX.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        MobileMenu.IsVisible = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
    }
}