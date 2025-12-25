/*
FIX: Provide a fast placeholder thumbnail for each SwarmUI prompt request.
CAUSE: Latest Images list previously had no entry until the final image downloaded.
CHANGE: Render a small bitmap with prompt snippet so each request is visible immediately. 2025-12-22
*/

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PromptLoom.Services;

public static class ThumbnailRenderer
{
    public static ImageSource RenderPromptThumbnail(string prompt, int width = 170, int height = 170)
    {
        prompt ??= "";
        var safe = prompt.Trim().Replace("\r", " ").Replace("\n", " ");
        if (safe.Length > 180) safe = safe[..180] + "…";

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bg = new SolidColorBrush(Color.FromRgb(32, 19, 36));
            bg.Freeze();
            dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, width, height), 12, 12);

            var accent = new SolidColorBrush(Color.FromRgb(255, 130, 200));
            accent.Freeze();
            dc.DrawRoundedRectangle(null, new Pen(accent, 2), new Rect(4, 4, width - 8, height - 8), 10, 10);

            var ft = new FormattedText(
                safe,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                12,
                Brushes.White,
                VisualTreeHelper.GetDpi(visual).PixelsPerDip);

            ft.MaxTextWidth = width - 18;
            ft.MaxTextHeight = height - 18;
            ft.Trimming = TextTrimming.CharacterEllipsis;

            dc.DrawText(ft, new Point(9, 9));

            var sub = new FormattedText(
                "SwarmUI gen…",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                11,
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                VisualTreeHelper.GetDpi(visual).PixelsPerDip);

            dc.DrawText(sub, new Point(9, height - 20));
        }

        var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }
}
