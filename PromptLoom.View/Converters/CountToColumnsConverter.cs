// CHANGE LOG
// - 2026-03-12 | Request: Title case | Add converter for title-cased file names.
// - 2026-03-12 | Request: File card colors | Convert item counts into UniformGrid column counts.

using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptLoom.Converters;

public sealed class CountToColumnsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return Math.Max(1, count);

        return 1;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class TitleCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
