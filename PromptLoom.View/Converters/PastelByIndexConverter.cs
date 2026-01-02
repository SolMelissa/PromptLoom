using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PromptLoom.Converters;

public sealed class PastelByIndexConverter : IValueConverter
{
    private static readonly Color[] Palette = new[]
    {
        (Color)ColorConverter.ConvertFromString("#FFFFF1F7"),
        (Color)ColorConverter.ConvertFromString("#FFF0FBFF"),
        (Color)ColorConverter.ConvertFromString("#FFF6F3FF"),
        (Color)ColorConverter.ConvertFromString("#FFFFF9EC"),
        (Color)ColorConverter.ConvertFromString("#FFF1FFFA"),
        (Color)ColorConverter.ConvertFromString("#FFF3F7FF"),
        (Color)ColorConverter.ConvertFromString("#FFFFF3F0"),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var idx = 0;
        if (value is int i) idx = i;
        var c = Palette[Math.Abs(idx) % Palette.Length];
        return new SolidColorBrush(c);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
