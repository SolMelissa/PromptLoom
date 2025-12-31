/*
FIX: Prompt tab prompt box needed to auto-size to its content while still allowing scrolling for very long prompts.
CAUSE: WPF TextBox does not auto-measure its height based on text by default, so the prompt area consumed too much/too little space.
CHANGE: Added a simple AutoSizeTextBox control that measures text height and caps it via MaxAutoHeight. 2025-12-24
*/

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PromptLoom.Controls;

/// <summary>
/// A TextBox that grows/shrinks to fit its content up to <see cref="MaxAutoHeight"/>, then enables vertical scrolling.
/// </summary>
public class AutoSizeTextBox : TextBox
{
    public static readonly DependencyProperty MaxAutoHeightProperty = DependencyProperty.Register(
        nameof(MaxAutoHeight),
        typeof(double),
        typeof(AutoSizeTextBox),
        new FrameworkPropertyMetadata(280d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Maximum height (in DIPs) this textbox will auto-size to before it starts scrolling.</summary>
    public double MaxAutoHeight
    {
        get => (double)GetValue(MaxAutoHeightProperty);
        set => SetValue(MaxAutoHeightProperty, value);
    }

    public AutoSizeTextBox()
    {
        TextWrapping = TextWrapping.Wrap;
        AcceptsReturn = true;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        TextChanged += (_, _) => InvalidateMeasure();
        SizeChanged += (_, _) => InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var baseSize = base.MeasureOverride(constraint);

        // If we don't have a meaningful width yet, fall back to base measurement.
        var availableWidth = double.IsInfinity(constraint.Width) ? ActualWidth : constraint.Width;
        if (availableWidth <= 20)
            return baseSize;

        // Measure the rendered height of the text with the current font settings.
        var ft = new FormattedText(
            Text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0, availableWidth - Padding.Left - Padding.Right - 10),
            Trimming = TextTrimming.None
        };

        // Add a little breathing room (padding + caret line) and clamp.
        var desired = ft.Height + Padding.Top + Padding.Bottom + 10;
        var clamped = Math.Min(MaxAutoHeight, Math.Max(MinHeight, desired));

        return new Size(baseSize.Width, clamped);
    }
}
