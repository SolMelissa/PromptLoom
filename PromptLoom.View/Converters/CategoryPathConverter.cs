// CHANGE LOG
// - 2026-03-02 | Request: Search file pills | Show path segment after Categories on file pills.
using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptLoom.Converters;

/// <summary>
/// Converts a full path into the portion after the Categories folder.
/// </summary>
public sealed class CategoryPathConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0)
            return value;

        var normalized = path.Replace('\\', '/');
        const string marker = "/Categories/";
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return normalized;

        var start = index + marker.Length;
        if (start >= normalized.Length)
            return string.Empty;

        return normalized[start..];
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? string.Empty;
}
