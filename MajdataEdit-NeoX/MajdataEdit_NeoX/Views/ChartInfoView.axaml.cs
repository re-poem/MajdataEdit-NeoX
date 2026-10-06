using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Models.Fumen;
using PropertyGenerator.Avalonia;
using System;
using System.IO;

namespace MajdataEdit_NeoX.Views
{
    public partial class ChartInfoView : UserControl
    {
        public ChartInfoView()
        {
            InitializeComponent();
        }




        [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
        public partial Bitmap Cover { get; set; } = null!;

        [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
        public partial MaidataFile Maidata { get; set; } = null!;

        [RelayCommand]
        public void AddCommand()
        {
            Maidata?.Commands.Add(new MutSimaiCommand("key", "value"));
        }

        [RelayCommand]
        public void DeleteCommand(MutSimaiCommand command)
        {
            Maidata?.Commands.Remove(command);
        }

        [RelayCommand]
        public void OpenBgCover()
        {
        }
        [RelayCommand]
        public void OpenBgVideo()
        {
        }
    }
}