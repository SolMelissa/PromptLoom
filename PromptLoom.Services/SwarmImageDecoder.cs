/*
FIX: Decode SwarmUI preview frames (data URLs / base64) into WPF ImageSource.
CAUSE: WebSocket streaming can deliver realtime previews as data:image/...;base64,... strings.
CHANGE: Added tolerant decoder for preview payloads. 2025-12-22
*/

using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PromptLoom.Services;

public static class SwarmImageDecoder
{
    public static ImageSource? TryDecodeToImageSource(string maybeDataUrlOrBase64)
    {
        if (string.IsNullOrWhiteSpace(maybeDataUrlOrBase64))
            return null;

        try
        {
            string b64 = maybeDataUrlOrBase64.Trim();

            // If it's a data URL: data:image/png;base64,AAA...
            var comma = b64.IndexOf(',');
            if (b64.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) && comma > 0)
                b64 = b64[(comma + 1)..];

            // If it looks like JSON-escaped, strip quotes.
            b64 = b64.Trim().Trim('"');

            var bytes = Convert.FromBase64String(b64);

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
