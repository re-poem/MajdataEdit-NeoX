using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MajSimai.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MajdataEdit_NeoX.Converters;

public class DiagnosticIconConverter : IValueConverter
{
    private static readonly Bitmap ErrorIcon = LoadIcon("error.png");
    private static readonly Bitmap WarnIcon = LoadIcon("warn.png");
    private static readonly Bitmap InfoIcon = LoadIcon("info.png");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SimaiDiagnosticSeverity.Error => ErrorIcon,
            SimaiDiagnosticSeverity.Warning => WarnIcon,
            _ => InfoIcon,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        AvaloniaProperty.UnsetValue;

    private static Bitmap LoadIcon(string name) =>
        new(AssetLoader.Open(new Uri($"avares://MajdataEdit_NeoX/Assets/Icons/ErrorList/{name}")));
}