using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CalendarGenerator.Core.Models;

namespace CalendarGenerator.App.Services;

public static class PhotoImageLoader
{
    public static BitmapSource? Load(PhotoItem photo, int decodePixelWidth = 800)
    {
        var path = !string.IsNullOrWhiteSpace(photo.ProjectCopyPath) && File.Exists(photo.ProjectCopyPath)
            ? photo.ProjectCopyPath!
            : photo.SourcePath;

        if (!File.Exists(path))
            return null;

        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = Math.Max(64, decodePixelWidth);
            bi.EndInit();
            bi.Freeze();

            if (IsFullFrame(photo))
                return bi;

            var x = (int)Math.Round(photo.CropX * bi.PixelWidth);
            var y = (int)Math.Round(photo.CropY * bi.PixelHeight);
            var w = (int)Math.Round(photo.CropWidth * bi.PixelWidth);
            var h = (int)Math.Round(photo.CropHeight * bi.PixelHeight);

            x = Math.Clamp(x, 0, Math.Max(0, bi.PixelWidth - 1));
            y = Math.Clamp(y, 0, Math.Max(0, bi.PixelHeight - 1));
            w = Math.Clamp(w, 1, bi.PixelWidth - x);
            h = Math.Clamp(h, 1, bi.PixelHeight - y);

            var cropped = new CroppedBitmap(bi, new Int32Rect(x, y, w, h));
            cropped.Freeze();
            return cropped;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFullFrame(PhotoItem photo) =>
        photo.CropX <= 0.001 &&
        photo.CropY <= 0.001 &&
        photo.CropWidth >= 0.999 &&
        photo.CropHeight >= 0.999;
}
