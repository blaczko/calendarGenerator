using CalendarGenerator.Core.Models;

namespace CalendarGenerator.Core.Photos;

/// <summary>
/// Justified-row collage with optional per-photo manual layout overrides.
/// Layout math uses the photo-area aspect so frames match each photo's pixel aspect ratio.
/// </summary>
public sealed class CollageEngine
{
    private const double Gap = 0.012;

    public CollageLayout Build(IReadOnlyList<PhotoItem> photos, double containerAspect = 1.6)
    {
        if (photos.Count == 0)
            return new CollageLayout { Slots = Array.Empty<CollageSlot>() };

        var aspect = SanitizeContainerAspect(containerAspect);
        var items = photos.Take(12).ToList();
        var auto = BuildAuto(items, aspect);
        var autoLookup = auto.Slots.ToDictionary(s => s.PhotoId);

        var slots = new List<CollageSlot>(items.Count);
        foreach (var photo in items)
        {
            if (photo.HasManualLayout && photo.LayoutWidth > 0.01 && photo.LayoutHeight > 0.01)
            {
                slots.Add(new CollageSlot
                {
                    PhotoId = photo.Id,
                    Bounds = new CollageRect(
                        Clamp01(photo.LayoutX),
                        Clamp01(photo.LayoutY),
                        Math.Clamp(photo.LayoutWidth, 0.05, 1),
                        Math.Clamp(photo.LayoutHeight, 0.05, 1))
                });
            }
            else if (autoLookup.TryGetValue(photo.Id, out var slot))
            {
                slots.Add(slot);
            }
        }

        return new CollageLayout { Slots = slots, Gap = Gap };
    }

    /// <summary>
    /// Writes the current automatic positions into each photo that has no manual layout yet.
    /// </summary>
    public void BakeAutomaticLayouts(IReadOnlyList<PhotoItem> photos, double containerAspect = 1.6)
    {
        var auto = BuildAuto(photos.Take(12).ToList(), SanitizeContainerAspect(containerAspect));
        foreach (var slot in auto.Slots)
        {
            var photo = photos.FirstOrDefault(p => p.Id == slot.PhotoId);
            if (photo is null || photo.HasManualLayout)
                continue;

            photo.LayoutX = slot.Bounds.X;
            photo.LayoutY = slot.Bounds.Y;
            photo.LayoutWidth = slot.Bounds.Width;
            photo.LayoutHeight = slot.Bounds.Height;
        }
    }

    public void ApplyManualLayout(PhotoItem photo, CollageRect bounds)
    {
        photo.HasManualLayout = true;
        photo.LayoutX = Clamp01(bounds.X);
        photo.LayoutY = Clamp01(bounds.Y);
        photo.LayoutWidth = Math.Clamp(bounds.Width, 0.05, 1);
        photo.LayoutHeight = Math.Clamp(bounds.Height, 0.05, 1);
    }

    /// <summary>
    /// Normalized width/height that yields <paramref name="photoAspect"/> in pixels
    /// inside a container of aspect width/height = <paramref name="containerAspect"/>.
    /// </summary>
    public static CollageRect KeepPixelAspect(
        CollageRect bounds,
        double photoAspect,
        double containerAspect)
    {
        var surface = SanitizeContainerAspect(containerAspect);
        var pixel = Math.Clamp(photoAspect, 0.35, 3.0);
        // (normW * W) / (normH * H) = pixel  =>  normW/normH = pixel / surface
        var normAspect = pixel / surface;

        var w = Math.Clamp(bounds.Width, 0.05, 1);
        var h = w / normAspect;
        if (h > 1)
        {
            h = 1;
            w = h * normAspect;
        }

        var x = Math.Clamp(bounds.X, 0, Math.Max(0, 1 - w));
        var y = Math.Clamp(bounds.Y, 0, Math.Max(0, 1 - h));
        return new CollageRect(x, y, w, h);
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    private static double SanitizeContainerAspect(double containerAspect) =>
        Math.Clamp(containerAspect is > 0.2 and < 8 ? containerAspect : 1.6, 0.35, 5.0);

    private static CollageLayout BuildAuto(IReadOnlyList<PhotoItem> photos, double containerAspect)
    {
        if (photos.Count == 0)
            return new CollageLayout { Slots = Array.Empty<CollageSlot>() };

        var items = photos
            .Select(p => (Photo: p, Aspect: Math.Clamp(p.AspectRatio, 0.35, 3.0)))
            .ToList();

        var rows = PartitionRows(items);
        return new CollageLayout { Slots = LayoutRows(rows, containerAspect), Gap = Gap };
    }

    private static List<List<(PhotoItem Photo, double Aspect)>> PartitionRows(
        List<(PhotoItem Photo, double Aspect)> items)
    {
        var n = items.Count;
        var rowCount = IdealRowCount(n, items.Average(i => i.Aspect));

        if (rowCount == 1)
            return [items];

        var totalAspect = items.Sum(i => i.Aspect);
        var target = totalAspect / rowCount;
        var rows = new List<List<(PhotoItem Photo, double Aspect)>>();
        var current = new List<(PhotoItem Photo, double Aspect)>();
        var currentSum = 0.0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var rowsLeft = rowCount - rows.Count;
            var itemsLeft = items.Count - i;

            var mustClose = current.Count > 0 && itemsLeft <= rowsLeft - 1;
            var wouldExceed = current.Count > 0
                              && currentSum + item.Aspect > target * 1.15
                              && rows.Count < rowCount - 1;

            if (mustClose || wouldExceed)
            {
                rows.Add(current);
                current = [];
                currentSum = 0;
            }

            current.Add(item);
            currentSum += item.Aspect;
        }

        if (current.Count > 0)
            rows.Add(current);

        while (rows.Count > rowCount)
        {
            var last = rows[^1];
            rows.RemoveAt(rows.Count - 1);
            rows[^1].AddRange(last);
        }

        return rows;
    }

    private static int IdealRowCount(int n, double avgAspect) =>
        n switch
        {
            1 => 1,
            2 => 1,
            3 => avgAspect < 0.9 ? 1 : 2,
            4 => 2,
            5 => 2,
            6 => 2,
            7 or 8 or 9 => 3,
            _ => 3
        };

    private static List<CollageSlot> LayoutRows(
        List<List<(PhotoItem Photo, double Aspect)>> rows,
        double containerAspect)
    {
        // In normalized space: normW = photoAspect / containerAspect * normH
        // so a full-width row: sum(normW) + gaps = 1 => normH = (1-gaps) * containerAspect / sum(photoAspect)
        var rawHeights = rows.Select(row =>
        {
            var gaps = Math.Max(0, row.Count - 1) * Gap;
            var aspectSum = row.Sum(p => p.Aspect);
            return (1.0 - gaps) * containerAspect / Math.Max(0.01, aspectSum);
        }).ToList();

        var rawTotal = rawHeights.Sum() + Math.Max(0, rows.Count - 1) * Gap;
        var scale = Math.Min(1.0, 1.0 / rawTotal);

        var contentH = rawTotal * scale;
        var contentW = scale; // widths already sum to ~1 before vertical fit; scale shrinks uniformly
        // When scale < 1, each normW also scales: w = (aspect/containerAspect)*h, h scaled => w scales.
        // Row width sum becomes scale, so center horizontally.
        var originX = (1.0 - contentW) / 2.0;
        var originY = (1.0 - contentH) / 2.0;

        var slots = new List<CollageSlot>();
        var y = originY;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var h = rawHeights[r] * scale;
            var x = originX;

            foreach (var (photo, aspect) in row)
            {
                var w = aspect / containerAspect * h;
                slots.Add(new CollageSlot
                {
                    PhotoId = photo.Id,
                    Bounds = new CollageRect(x, y, w, h)
                });
                x += w + Gap * scale;
            }

            y += h + Gap * scale;
        }

        return slots;
    }
}
