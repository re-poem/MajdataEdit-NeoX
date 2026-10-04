
using Avalonia;
using Avalonia.Controls;
using Avalonia.PropertyGrid.Services;
using Avalonia.Reactive;
using PropertyModels.Localization;
using System.Collections.Generic;

namespace MajdataEdit_NeoX.Views;

public partial class MainView : UserControl
{
    private readonly List<ICultureData> _allCultures = [];

    public ICultureData[] AllCultures => [.. _allCultures];

    public static readonly StyledProperty<ICultureData> CurrentCultureProperty =
        AvaloniaProperty.Register<MainView, ICultureData>(
            nameof(CurrentCulture),
            defaultValue: LocalizationService.Default.CultureData);
    public ICultureData CurrentCulture
    {
        get => GetValue(CurrentCultureProperty);
        set => SetValue(CurrentCultureProperty, value);
    }

    public MainView()
    {
        LocalizationService.Default.AddExtraService(new MajLocalizationService());
        _allCultures.AddRange(LocalizationService.Default.GetCultures());

        InitializeComponent();

        // Subscribe to culture changes
        this.GetObservable(CurrentCultureProperty)
            .Subscribe(new AnonymousObserver<ICultureData>(OnCurrentCultureChanged));
    }

    private static void OnCurrentCultureChanged(ICultureData? newCulture)
    {
        if (newCulture != null)
        {
            LocalizationService.Default.SelectCulture(newCulture.Culture.Name);
        }
    }
}