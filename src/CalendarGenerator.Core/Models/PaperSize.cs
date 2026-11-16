namespace CalendarGenerator.Core.Models;

public enum PaperSizeKind
{
    A4,
    A3,
    A5,
    Custom
}

public sealed class PaperSize
{
    public PaperSizeKind Kind { get; set; } = PaperSizeKind.A4;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;

    /// <summary>Width in millimeters.</summary>
    public double WidthMm { get; set; } = 210;

    /// <summary>Height in millimeters.</summary>
    public double HeightMm { get; set; } = 297;

    /// <summary>Short label for filenames / UI (A4, A3, A5, Custom).</summary>
    public string SizeLabel => Kind switch
    {
        PaperSizeKind.A3 => "A3",
        PaperSizeKind.A5 => "A5",
        PaperSizeKind.Custom => "Custom",
        _ => "A4"
    };

    /// <summary>portrait or landscape — for export filenames.</summary>
    public string OrientationLabel =>
        Orientation == PageOrientation.Landscape ? "landscape" : "portrait";

    public void ApplyPreset()
    {
        if (Kind == PaperSizeKind.Custom)
            return;

        var (w, h) = Kind switch
        {
            PaperSizeKind.A3 => (297.0, 420.0),
            PaperSizeKind.A5 => (148.0, 210.0),
            _ => (210.0, 297.0)
        };

        if (Orientation == PageOrientation.Portrait)
        {
            WidthMm = w;
            HeightMm = h;
        }
        else
        {
            WidthMm = h;
            HeightMm = w;
        }
    }
}
