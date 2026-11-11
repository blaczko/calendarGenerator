using System.IO;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Orders;

namespace CalendarGenerator.App.Services;

public sealed class OrderProjectLoader
{
    private static readonly string[] MonthFolderPrefixes =
    [
        "01_", "02_", "03_", "04_", "05_", "06_",
        "07_", "08_", "09_", "10_", "11_", "12_"
    ];

    public void ApplyOrderToProject(CalendarProject project, OrderRecord order)
    {
        project.Name = string.IsNullOrWhiteSpace(order.FullName)
            ? order.OrderId
            : $"{order.OrderId}_{order.FullName}";
        project.Year = order.CalendarYear > 2000 ? order.CalendarYear : project.Year;
        project.Paper.Orientation = string.Equals(order.CalendarFormatPref, "landscape", StringComparison.OrdinalIgnoreCase)
            ? PageOrientation.Landscape
            : PageOrientation.Portrait;
        project.Paper.ApplyPreset();

        project.Events.Clear();
        foreach (var ev in order.Events)
        {
            if (string.IsNullOrWhiteSpace(ev.Description)) continue;
            if (!TryParseEventDate(ev.EventDate, out var month, out var day)) continue;
            project.Events.Add(new CustomEvent
            {
                Month = month,
                Day = day,
                Description = ev.Description.Trim()
            });
        }

        if (string.IsNullOrWhiteSpace(order.LocalFolderPath) || !Directory.Exists(order.LocalFolderPath))
            return;

        for (var month = 1; month <= 12; month++)
        {
            var monthModel = project.GetMonth(month);
            monthModel.Photos.Clear();
            var folder = FindMonthFolder(order.LocalFolderPath, month);
            if (folder is null) continue;

            var files = Directory.GetFiles(folder)
                .Where(IsImage)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in files.Take(6))
            {
                monthModel.Photos.Add(new PhotoItem
                {
                    Id = Guid.NewGuid(),
                    SourcePath = file,
                    AspectRatio = TryReadAspectRatio(file)
                });
            }
        }
    }

    /// <summary>
    /// Accepts ISO yyyy-MM-dd (web form), culture-specific dates, or MM-dd fragments.
    /// </summary>
    internal static bool TryParseEventDate(string? raw, out int month, out int day)
    {
        month = 0;
        day = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();
        if (DateTime.TryParseExact(
                text,
                ["yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd", "dd.MM.yyyy", "dd/MM/yyyy"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var exact))
        {
            month = exact.Month;
            day = exact.Day;
            return true;
        }

        if (DateTime.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out var inv))
        {
            month = inv.Month;
            day = inv.Day;
            return true;
        }

        if (DateTime.TryParse(text, out var local))
        {
            month = local.Month;
            day = local.Day;
            return true;
        }

        // month/day only (rare)
        var parts = text.Split(['-', '.', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var m)
            && int.TryParse(parts[1], out var d)
            && m is >= 1 and <= 12
            && d is >= 1 and <= 31)
        {
            month = m;
            day = d;
            return true;
        }

        return false;
    }

    private static double TryReadAspectRatio(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                System.Windows.Media.Imaging.BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            if (frame.PixelHeight <= 0) return 1.0;
            return (double)frame.PixelWidth / frame.PixelHeight;
        }
        catch
        {
            return 1.0;
        }
    }

    private static string? FindMonthFolder(string orderRoot, int month)
    {
        var prefix = MonthFolderPrefixes[month - 1];
        var photosRoot = Path.Combine(orderRoot, "photos");
        var searchRoots = Directory.Exists(photosRoot)
            ? new[] { orderRoot, photosRoot }
            : new[] { orderRoot };

        foreach (var root in searchRoots)
        {
            var match = Directory.GetDirectories(root)
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return null;
    }

    private static bool IsImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp";
    }
}
