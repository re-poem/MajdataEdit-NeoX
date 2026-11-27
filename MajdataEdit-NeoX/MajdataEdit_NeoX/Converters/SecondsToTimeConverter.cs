using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace MajdataEdit_NeoX.Converters;

public sealed class SecondsToTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return $"{(int)time.TotalMinutes:0}:{time.Seconds:00}";
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}