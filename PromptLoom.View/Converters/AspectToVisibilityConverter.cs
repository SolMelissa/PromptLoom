using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PromptLoom.Converters;

/// <summary>
/// Returns Visible when (width &lt; height) by default (portrait), otherwise Collapsed.
/// Set parameter to "invert" to swap behavior.
/// </summary>
public sealed class AspectToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (values.Length < 2) return Visibility.Visible;
            var w = values[0] is double dw ? dw : 0.0;
            var h = values[1] is double dh ? dh : 0.0;

            var portrait = w > 0 && h > 0 && w < h;
            var invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
            var visible = invert ? !portrait : portrait;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            return Visibility.Visible;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
