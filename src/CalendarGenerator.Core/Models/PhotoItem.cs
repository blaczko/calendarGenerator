namespace CalendarGenerator.Core.Models;

public sealed class PhotoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Absolute path to the source image file.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Optional path to a project-local copy.</summary>
    public string? ProjectCopyPath { get; set; }

    public double AspectRatio { get; set; } = 1.0;
    public bool IsPortrait => AspectRatio < 1.0;

    /// <summary>Normalized crop rectangle (0–1) relative to the source image.</summary>
    public double CropX { get; set; }
    public double CropY { get; set; }
    public double CropWidth { get; set; } = 1.0;
    public double CropHeight { get; set; } = 1.0;

    /// <summary>Manual offset within the assigned collage slot (0–1, 0.5 = centered).</summary>
    public double OffsetX { get; set; } = 0.5;
    public double OffsetY { get; set; } = 0.5;

    public double Zoom { get; set; } = 1.0;

    /// <summary>When true, Layout* overrides the automatic collage slot.</summary>
    public bool HasManualLayout { get; set; }

    /// <summary>Normalized collage bounds (0–1) when HasManualLayout is set.</summary>
    public double LayoutX { get; set; }
    public double LayoutY { get; set; }
    public double LayoutWidth { get; set; } = 0.3;
    public double LayoutHeight { get; set; } = 0.3;
}
