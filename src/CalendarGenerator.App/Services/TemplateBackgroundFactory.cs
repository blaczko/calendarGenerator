using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CalendarGenerator.Core.Templates;

namespace CalendarGenerator.App.Services;

public static class TemplateBackgroundFactory
{
    public static Brush Create(TemplateDefinition template, double width, double height)
    {
        if (template.Style == TemplateStyle.CustomImage ||
            !string.IsNullOrWhiteSpace(template.BackgroundImagePath) ||
            !string.IsNullOrWhiteSpace(template.ProjectCopyPath))
        {
            return CustomImageBackground(template, width, height);
        }

        var primary = (Color)ColorConverter.ConvertFromString(template.PrimaryHex)!;
        var secondary = (Color)ColorConverter.ConvertFromString(template.SecondaryHex)!;
        var accent = (Color)ColorConverter.ConvertFromString(template.AccentHex)!;

        return template.Style switch
        {
            TemplateStyle.PastelGradient => Gradient(primary, secondary, accent, width, height),
            TemplateStyle.Marble => Marble(primary, secondary, accent, width, height),
            TemplateStyle.Geometric => Geometric(primary, secondary, accent, width, height),
            TemplateStyle.PastelWaves => Waves(primary, secondary, accent, width, height),
            TemplateStyle.Scandinavian => Scandinavian(primary, secondary, accent, width, height),
            TemplateStyle.Linen => Linen(primary, secondary, accent, width, height),
            TemplateStyle.EarthTones => SoftOrganic(primary, secondary, accent, width, height),
            TemplateStyle.Watercolor => Watercolor(primary, secondary, accent, width, height),
            TemplateStyle.BotanicalAbstract => Botanical(primary, secondary, accent, width, height),
            TemplateStyle.PaperTexture => Paper(primary, secondary, accent, width, height),
            TemplateStyle.Monochrome => Monochrome(primary, secondary, accent, width, height),
            TemplateStyle.Aurora => Aurora(primary, secondary, accent, width, height),
            TemplateStyle.CraftPaper => CraftPaper(primary, secondary, accent, width, height),
            TemplateStyle.SolidWash => SolidWash(primary, secondary, accent, width, height),
            _ => SoftOrganic(primary, secondary, accent, width, height)
        };
    }

    public static Brush Create(PageTheme theme, double width, double height) =>
        Create(theme.AsBackgroundTemplate(), width, height);

    private static Brush CustomImageBackground(TemplateDefinition template, double width, double height)
    {
        var path = !string.IsNullOrWhiteSpace(template.ProjectCopyPath) && File.Exists(template.ProjectCopyPath)
            ? template.ProjectCopyPath
            : template.BackgroundImagePath;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // Fallback if the image is missing
            return SoftOrganic(
                (Color)ColorConverter.ConvertFromString(template.PrimaryHex)!,
                (Color)ColorConverter.ConvertFromString(template.SecondaryHex)!,
                (Color)ColorConverter.ConvertFromString(template.AccentHex)!,
                width, height);
        }

        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = Math.Max(800, (int)width);
            bi.EndInit();
            bi.Freeze();

            var imageBrush = new ImageBrush(bi)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            // Soft light veil so calendar text stays readable on busy photos.
            var overlay = new DrawingGroup();
            overlay.Children.Add(new GeometryDrawing(imageBrush, null, new RectangleGeometry(new Rect(0, 0, width, height))));
            overlay.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromArgb(48, 255, 252, 247)),
                null,
                new RectangleGeometry(new Rect(0, 0, width, height))));
            return new DrawingBrush(overlay) { Stretch = Stretch.Fill };
        }
        catch
        {
            return SoftOrganic(
                (Color)ColorConverter.ConvertFromString("#EFE8DE")!,
                (Color)ColorConverter.ConvertFromString("#D9CDBE")!,
                (Color)ColorConverter.ConvertFromString("#BFAE9A")!,
                width, height);
        }
    }

    private static Brush SoftOrganic(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        dg.Children.Add(RadialWash(b, w * 0.15, h * 0.05, w * 0.7, h * 0.55, 0.45));
        dg.Children.Add(Blob(b, w * -0.05, h * 0.55, w * 0.55, h * 0.55, 0.38));
        dg.Children.Add(Blob(c, w * 0.55, h * 0.35, w * 0.55, h * 0.7, 0.32));
        dg.Children.Add(Blob(b, w * 0.25, h * -0.05, w * 0.5, h * 0.35, 0.25));
        dg.Children.Add(Blob(WithAlpha(c, 90), w * 0.7, h * 0.05, w * 0.4, h * 0.3, 0.5));
        return BrushOf(dg);
    }

    private static Brush Gradient(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(a, 0),
                    new(b, 0.45),
                    new(c, 1)
                },
                new Point(0, 0),
                new Point(1, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, w, h))));
        dg.Children.Add(RadialWash(Colors.White, w * 0.2, h * 0.1, w * 0.6, h * 0.4, 0.22));
        dg.Children.Add(Blob(WithAlpha(c, 100), w * 0.55, h * 0.55, w * 0.5, h * 0.45, 0.35));
        return BrushOf(dg);
    }

    private static Brush Watercolor(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        dg.Children.Add(Blob(WithAlpha(b, 110), w * 0.0, h * 0.0, w * 0.75, h * 0.45, 0.55));
        dg.Children.Add(Blob(WithAlpha(c, 95), w * 0.35, h * 0.25, w * 0.7, h * 0.55, 0.5));
        dg.Children.Add(Blob(WithAlpha(b, 80), w * -0.1, h * 0.55, w * 0.6, h * 0.55, 0.45));
        dg.Children.Add(Blob(WithAlpha(c, 70), w * 0.5, h * 0.65, w * 0.55, h * 0.45, 0.4));
        dg.Children.Add(RadialWash(Colors.White, w * 0.3, h * 0.35, w * 0.4, h * 0.3, 0.18));
        return BrushOf(dg);
    }

    private static Brush Geometric(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        var thin = new Pen(new SolidColorBrush(WithAlpha(c, 55)), Math.Max(1, w * 0.0012));
        var thick = new Pen(new SolidColorBrush(WithAlpha(b, 70)), Math.Max(1.5, w * 0.002));

        for (var i = 0; i < 10; i++)
        {
            var x = w * (0.05 + i * 0.1);
            dg.Children.Add(new GeometryDrawing(null, thin, new LineGeometry(new Point(x, 0), new Point(x, h))));
        }
        for (var i = 0; i < 8; i++)
        {
            var y = h * (0.06 + i * 0.12);
            dg.Children.Add(new GeometryDrawing(null, thin, new LineGeometry(new Point(0, y), new Point(w, y))));
        }

        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 55)), null,
            new RectangleGeometry(new Rect(w * 0.08, h * 0.12, w * 0.28, h * 0.22), 4, 4)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(c, 45)), null,
            new EllipseGeometry(new Point(w * 0.78, h * 0.22), w * 0.14, h * 0.1)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 40)), null,
            new RectangleGeometry(new Rect(w * 0.55, h * 0.62, w * 0.35, h * 0.25), 6, 6)));
        dg.Children.Add(new GeometryDrawing(null, thick,
            new LineGeometry(new Point(w * 0.1, h * 0.85), new Point(w * 0.45, h * 0.85))));
        return BrushOf(dg);
    }

    private static Brush Waves(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        for (var i = 0; i < 7; i++)
        {
            var y0 = h * (0.08 + i * 0.13);
            var amp = h * (0.04 + (i % 3) * 0.015);
            var path = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(0, y0) };
            fig.Segments.Add(new BezierSegment(
                new Point(w * 0.25, y0 - amp),
                new Point(w * 0.5, y0 + amp),
                new Point(w * 0.75, y0 - amp * 0.6), true));
            fig.Segments.Add(new BezierSegment(
                new Point(w * 0.9, y0 + amp * 0.4),
                new Point(w * 0.97, y0),
                new Point(w, y0), true));
            path.Figures.Add(fig);
            var color = i % 2 == 0 ? b : c;
            dg.Children.Add(new GeometryDrawing(null,
                new Pen(new SolidColorBrush(WithAlpha(color, (byte)(70 + i * 8))), Math.Max(2, h * 0.01)),
                path));
        }
        dg.Children.Add(RadialWash(Colors.White, w * 0.15, h * 0.2, w * 0.5, h * 0.35, 0.15));
        return BrushOf(dg);
    }

    private static Brush Scandinavian(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        var dot = new SolidColorBrush(WithAlpha(c, 90));
        var size = Math.Min(w, h) * 0.012;
        for (var y = 0; y < 14; y++)
        for (var x = 0; x < 16; x++)
        {
            if ((x + y) % 2 != 0) continue;
            dg.Children.Add(new GeometryDrawing(dot, null,
                new EllipseGeometry(
                    new Point(w * (0.04 + x * 0.06), h * (0.04 + y * 0.07)),
                    size, size)));
        }
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 70)), null,
            new RectangleGeometry(new Rect(w * 0.08, h * 0.12, w * 0.32, h * 0.28), 2, 2)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(c, 55)), null,
            new EllipseGeometry(new Point(w * 0.78, h * 0.7), w * 0.16, h * 0.14)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 45)), null,
            new RectangleGeometry(new Rect(w * 0.55, h * 0.18, w * 0.3, h * 0.12), 2, 2)));
        return BrushOf(dg);
    }

    private static Brush Linen(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        var penV = new Pen(new SolidColorBrush(WithAlpha(b, 40)), 0.9);
        var penH = new Pen(new SolidColorBrush(WithAlpha(c, 35)), 0.9);
        var step = Math.Max(6, w * 0.012);
        for (double x = 0; x < w; x += step)
            dg.Children.Add(new GeometryDrawing(null, penV, new LineGeometry(new Point(x, 0), new Point(x, h))));
        for (double y = 0; y < h; y += step)
            dg.Children.Add(new GeometryDrawing(null, penH, new LineGeometry(new Point(0, y), new Point(w, y))));
        dg.Children.Add(RadialWash(b, w * 0.4, h * 0.3, w * 0.5, h * 0.4, 0.12));
        return BrushOf(dg);
    }

    private static Brush Paper(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        var rng = new Random(42);
        for (var i = 0; i < 180; i++)
        {
            var brush = new SolidColorBrush(WithAlpha(i % 3 == 0 ? b : c, (byte)rng.Next(18, 40)));
            dg.Children.Add(new GeometryDrawing(brush, null,
                new EllipseGeometry(
                    new Point(rng.NextDouble() * w, rng.NextDouble() * h),
                    rng.NextDouble() * 1.8 + 0.4,
                    rng.NextDouble() * 1.8 + 0.4)));
        }
        dg.Children.Add(Blob(WithAlpha(b, 50), w * 0.6, h * 0.05, w * 0.45, h * 0.35, 0.35));
        dg.Children.Add(Blob(WithAlpha(c, 40), w * -0.05, h * 0.65, w * 0.45, h * 0.4, 0.3));
        return BrushOf(dg);
    }

    private static Brush Marble(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(a, 0),
                    new(b, 0.28),
                    new(a, 0.5),
                    new(c, 0.72),
                    new(a, 1)
                },
                new Point(0.05, 0),
                new Point(0.95, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, w, h))));

        for (var i = 0; i < 5; i++)
        {
            var path = new PathGeometry();
            var y = h * (0.15 + i * 0.16);
            var fig = new PathFigure { StartPoint = new Point(0, y) };
            fig.Segments.Add(new BezierSegment(
                new Point(w * 0.3, y + h * 0.05),
                new Point(w * 0.6, y - h * 0.04),
                new Point(w, y + h * 0.02), true));
            path.Figures.Add(fig);
            dg.Children.Add(new GeometryDrawing(null,
                new Pen(new SolidColorBrush(WithAlpha(Colors.White, 90)), Math.Max(1.2, w * 0.002)),
                path));
        }
        return BrushOf(dg);
    }

    private static Brush Botanical(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        for (var i = 0; i < 7; i++)
        {
            var xBase = w * (0.05 + i * 0.14);
            var path = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(xBase, h) };
            fig.Segments.Add(new BezierSegment(
                new Point(xBase - w * 0.04, h * 0.6),
                new Point(xBase + w * 0.08, h * 0.35),
                new Point(xBase + w * 0.02, h * 0.08), true));
            path.Figures.Add(fig);
            dg.Children.Add(new GeometryDrawing(null,
                new Pen(new SolidColorBrush(WithAlpha(c, 70)), Math.Max(1.4, w * 0.0035)),
                path));

            // abstract petal-like ovals (not recognizable plants)
            dg.Children.Add(Blob(WithAlpha(b, 70),
                xBase - w * 0.04, h * (0.18 + (i % 3) * 0.12),
                w * 0.1, h * 0.08, 0.55));
            dg.Children.Add(Blob(WithAlpha(c, 55),
                xBase + w * 0.01, h * (0.4 + (i % 2) * 0.1),
                w * 0.08, h * 0.06, 0.45));
        }
        dg.Children.Add(RadialWash(Colors.White, w * 0.25, h * 0.45, w * 0.5, h * 0.35, 0.12));
        return BrushOf(dg);
    }

    private static Brush Monochrome(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 70)), null,
            new EllipseGeometry(new Point(w * 0.78, h * 0.18), w * 0.3, h * 0.2)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(c, 55)), null,
            new RectangleGeometry(new Rect(w * 0.06, h * 0.55, w * 0.42, h * 0.32), 8, 8)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(b, 40)), null,
            new EllipseGeometry(new Point(w * 0.35, h * 0.25), w * 0.18, h * 0.12)));
        dg.Children.Add(new GeometryDrawing(new SolidColorBrush(WithAlpha(c, 35)), null,
            new RectangleGeometry(new Rect(w * 0.58, h * 0.62, w * 0.28, h * 0.2), 4, 4)));
        return BrushOf(dg);
    }

    private static Brush Aurora(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(a, 0),
                    new(b, 0.4),
                    new(c, 0.75),
                    new(a, 1)
                },
                new Point(0, 0),
                new Point(1, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, w, h))));
        dg.Children.Add(RadialWash(Colors.White, w * 0.1, h * 0.05, w * 0.55, h * 0.4, 0.28));
        dg.Children.Add(Blob(WithAlpha(c, 90), w * 0.45, h * 0.4, w * 0.6, h * 0.55, 0.35));
        dg.Children.Add(Blob(WithAlpha(b, 80), w * -0.05, h * 0.55, w * 0.5, h * 0.5, 0.3));
        return BrushOf(dg);
    }

    private static Brush CraftPaper(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(Fill(a, w, h));
        var rng = new Random(77);
        for (var i = 0; i < 220; i++)
        {
            var brush = new SolidColorBrush(WithAlpha(i % 2 == 0 ? b : c, (byte)rng.Next(16, 42)));
            dg.Children.Add(new GeometryDrawing(brush, null,
                new EllipseGeometry(
                    new Point(rng.NextDouble() * w, rng.NextDouble() * h),
                    rng.NextDouble() * 2.2 + 0.3,
                    rng.NextDouble() * 2.2 + 0.3)));
        }
        dg.Children.Add(Blob(WithAlpha(b, 55), w * 0.55, h * -0.05, w * 0.55, h * 0.4, 0.3));
        dg.Children.Add(Blob(WithAlpha(c, 45), w * -0.1, h * 0.6, w * 0.5, h * 0.45, 0.28));
        return BrushOf(dg);
    }

    private static Brush SolidWash(Color a, Color b, Color c, double w, double h)
    {
        var dg = new DrawingGroup();
        dg.Children.Add(new GeometryDrawing(
            new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(a, 0),
                    new(b, 0.55),
                    new(c, 1)
                },
                new Point(0.5, 0),
                new Point(0.5, 1)),
            null,
            new RectangleGeometry(new Rect(0, 0, w, h))));
        dg.Children.Add(RadialWash(Colors.White, w * 0.2, h * 0.15, w * 0.6, h * 0.35, 0.12));
        return BrushOf(dg);
    }

    private static GeometryDrawing Fill(Color color, double w, double h) =>
        new(new SolidColorBrush(color), null, new RectangleGeometry(new Rect(0, 0, w, h)));

    private static GeometryDrawing RadialWash(Color color, double x, double y, double w, double h, double opacity)
    {
        var brush = new RadialGradientBrush(
            Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B),
            Color.FromArgb(0, color.R, color.G, color.B))
        {
            Center = new Point(0.5, 0.5),
            RadiusX = 0.7,
            RadiusY = 0.7
        };
        return new GeometryDrawing(brush, null, new EllipseGeometry(new Point(x + w / 2, y + h / 2), w / 2, h / 2));
    }

    private static GeometryDrawing Blob(Color color, double x, double y, double w, double h, double opacity)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B));
        var geometry = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(x + w * 0.5, y), IsClosed = true };
        fig.Segments.Add(new BezierSegment(
            new Point(x + w * 0.92, y + h * 0.05),
            new Point(x + w, y + h * 0.4),
            new Point(x + w * 0.82, y + h * 0.75), true));
        fig.Segments.Add(new BezierSegment(
            new Point(x + w * 0.6, y + h),
            new Point(x + w * 0.22, y + h * 0.95),
            new Point(x + w * 0.05, y + h * 0.55), true));
        fig.Segments.Add(new BezierSegment(
            new Point(x, y + h * 0.22),
            new Point(x + w * 0.2, y),
            new Point(x + w * 0.5, y), true));
        geometry.Figures.Add(fig);
        return new GeometryDrawing(brush, null, geometry);
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static DrawingBrush BrushOf(DrawingGroup dg) =>
        new(dg) { Stretch = Stretch.Fill, TileMode = TileMode.None };
}
