using System.Windows;
using System.Windows.Media;

namespace CalendarGenerator.App.Services;

public static class HandDrawnGeometry
{
    /// <summary>
    /// Creates a slightly imperfect ellipse path resembling a red pen circle.
    /// </summary>
    public static PathGeometry CreateCircle(Rect bounds, int seed)
    {
        var rng = new Random(seed);
        var cx = bounds.X + bounds.Width / 2;
        var cy = bounds.Y + bounds.Height / 2;
        var rx = bounds.Width / 2;
        var ry = bounds.Height / 2;

        const int points = 18;
        var fig = new PathFigure { IsClosed = true };
        Point? first = null;

        for (var i = 0; i <= points; i++)
        {
            var t = i / (double)points * Math.PI * 2;
            var jitterR = 1 + (rng.NextDouble() - 0.5) * 0.08;
            var jitterA = (rng.NextDouble() - 0.5) * 0.05;
            var x = cx + Math.Cos(t + jitterA) * rx * jitterR;
            var y = cy + Math.Sin(t + jitterA) * ry * jitterR * (0.96 + rng.NextDouble() * 0.08);
            var p = new Point(x, y);
            if (first is null)
            {
                fig.StartPoint = p;
                first = p;
            }
            else
            {
                fig.Segments.Add(new LineSegment(p, true));
            }
        }

        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return geo;
    }
}
