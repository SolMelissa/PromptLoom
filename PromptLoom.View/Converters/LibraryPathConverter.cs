// CHANGE LOG
// - 2026-03-06 | Request: Tag-only mode | Show path segment after Library on file pills.

using System;
using System.Globalization;
using System.Windows.Data;

namespace PromptLoom.Converters;

/// <summary>
/// Converts a full path into the portion after the Library folder.
/// </summary>
public sealed class LibraryPathConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0)
            return value;

        var normalized = path.Replace('\\', '/');
        const string marker = "/Library/";
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
