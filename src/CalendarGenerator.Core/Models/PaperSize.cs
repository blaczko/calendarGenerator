namespace CalendarGenerator.Core.Models;

public enum PaperSizeKind
{
    A4,
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

    public void ApplyPreset()
    {
        if (Kind != PaperSizeKind.A4)
            return;

        if (Orientation == PageOrientation.Portrait)
        {
            WidthMm = 210;
            HeightMm = 297;
        }
        else
        {
            WidthMm = 297;
            HeightMm = 210;
        }
    }
}
