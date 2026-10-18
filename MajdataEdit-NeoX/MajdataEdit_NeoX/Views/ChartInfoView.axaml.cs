using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.PropertyGrid.Services;
using CommunityToolkit.Mvvm.Input;
using MajdataEdit_NeoX.Lang;
using MajdataEdit_NeoX.Models.Fumen;
using PropertyGenerator.Avalonia;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MajdataEdit_NeoX.Views
{
    public partial class ChartInfoView : UserControl
    {
        public ChartInfoView()
        {
            InitializeComponent();
        }




        [GeneratedDirectProperty(DefaultBindingMode = BindingMode.TwoWay)]
        public partial Bitmap? Cover { get; set; }

        [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
        public partial ICommand? ImportBgCover { get; set; }

        [GeneratedDirectProperty(DefaultBindingMode = BindingMode.OneWay)]
        public partial ICommand? ImportBgVideo { get; set; }

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
        public async Task OpenBgCoverAsync()
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocalizationService.Default[LangKeys.Gui_SelectBgCover],
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
            });

            if (files.Count == 0) return;
            if (files[0].TryGetLocalPath() is { } path && ImportBgCover?.CanExecute(path) == true)
                ImportBgCover.Execute(path);
        }

        [RelayCommand]
        public async Task OpenBgVideoAsync()
        {
            var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null) return;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = LocalizationService.Default[LangKeys.Gui_SelectBgVideo],
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Video") { Patterns = ["*.mp4", "*.webm", "*.avi", "*.mov"] }],
            });

            if (files.Count == 0) return;
            if (files[0].TryGetLocalPath() is { } path && ImportBgVideo?.CanExecute(path) == true)
                ImportBgVideo.Execute(path);
        }
    }
}