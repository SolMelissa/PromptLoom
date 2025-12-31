/*
FIX: Prompt/batch labels in the Prompt tab should display as a single line with no embedded newlines.
CAUSE: Prompts commonly include line breaks and WPF TextBlocks will wrap/stack them, making batch headers and separators messy.
CHANGE: Added OneLineTextConverter which replaces CR/LF with spaces and trims. 2025-12-24
*/

using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptLoom.Converters;

public sealed class OneLineTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var s = value.ToString() ?? string.Empty;
        return s
            .Replace("\r\n", " ")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value;
}
