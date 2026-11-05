using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CalendarGenerator.App.Services;

public static class WatermarkLogoService
{
    /// <summary>
    /// Loads the embedded logo and writes a PNG with near-white background removed.
    /// </summary>
    public static string EnsureTransparentLogoPng()
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatholicCalendarCreator",
            "assets");
        Directory.CreateDirectory(cacheDir);
        var outPath = Path.Combine(cacheDir, "zatai-pixel-logo-transparent.png");
        var source = ResolveSourceLogoPath();
        if (source is null || !File.Exists(source))
            throw new FileNotFoundException("ZATAI PIXEL logó nem található.", source);

        if (File.Exists(outPath) &&
            new FileInfo(outPath).Length > 0 &&
            File.GetLastWriteTimeUtc(outPath) >= File.GetLastWriteTimeUtc(source))
            return outPath;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(source, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var width = converted.PixelWidth;
        var height = converted.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[height * stride];
        converted.CopyPixels(pixels, stride, 0);

        // Near-white chroma key → transparent; keep logo faintly watermarked (~22% opacity)
        const double watermarkAlpha = 0.22;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (r >= 245 && g >= 245 && b >= 245)
            {
                pixels[i + 3] = 0;
            }
            else if (r >= 235 && g >= 235 && b >= 235)
            {
                var edge = Math.Clamp((int)((255 - ((r + g + b) / 3.0)) * 8), 0, 255);
                pixels[i + 3] = (byte)Math.Clamp((int)(edge * watermarkAlpha), 0, 255);
            }
            else
            {
                pixels[i + 3] = (byte)Math.Clamp((int)(255 * watermarkAlpha), 0, 255);
            }
        }

        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(result));
        using var fs = File.Create(outPath);
        encoder.Save(fs);
        return outPath;
    }

    private static string? ResolveSourceLogoPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Assets", "zatai-pixel-logo.jpg"),
            Path.Combine(baseDir, "zatai-pixel-logo.jpg"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "zatai-pixel-logo.jpg"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
