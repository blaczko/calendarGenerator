using System.IO;
using System.Windows.Media.Imaging;

namespace CalendarGenerator.App.Services;

public static class PhotoImportService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static bool IsSupported(string path) =>
        Supported.Contains(Path.GetExtension(path));

    /// <summary>
    /// Validates the image and returns a WPF-loadable path with aspect ratio (width/height).
    /// WebP works when a Windows WebP codec is installed (common on Win10/11).
    /// </summary>
    public static Task<(string Path, double AspectRatio)> PrepareAsync(
        string sourcePath,
        string? cacheDir = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("A kép nem található.", sourcePath);

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(sourcePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];
            var ratio = frame.PixelWidth / (double)Math.Max(1, frame.PixelHeight);
            return (sourcePath, ratio);
        }, ct);
    }
}
