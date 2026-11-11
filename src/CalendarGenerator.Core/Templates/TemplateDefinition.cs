namespace CalendarGenerator.Core.Templates;

public sealed class TemplateDefinition
{
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required string PrimaryHex { get; set; }
    public required string SecondaryHex { get; set; }
    public required string AccentHex { get; set; }
    public required TemplateStyle Style { get; set; }

    /// <summary>Absolute path to a user-uploaded background image (custom templates).</summary>
    public string? BackgroundImagePath { get; set; }

    /// <summary>Optional project-local copy of the background image.</summary>
    public string? ProjectCopyPath { get; set; }

    public CalendarFormat CalendarFormat { get; set; } = CalendarFormat.Classic;
    public string FontFamily { get; set; } = "Georgia";
    public double TitleFontSize { get; set; } = 32;
    public double YearFontSize { get; set; } = 15;
    public double DayFontSize { get; set; } = 18;
    public double WeekdayFontSize { get; set; } = 12;
    public double NameDayFontSize { get; set; } = 8.5;
    public string TitleColorHex { get; set; } = "#2C2C2C";
    public string YearColorHex { get; set; } = "#555555";
    public string DayColorHex { get; set; } = "#222222";
    public string WeekdayColorHex { get; set; } = "#222222";
    public string NameDayColorHex { get; set; } = "#5A554C";
    public string EventColorHex { get; set; } = "#C62828";
    public string GridLineHex { get; set; } = "#88C9BEAE";
    public string WeekendFillHex { get; set; } = "#66F3EEE6";
    public string HeaderFillHex { get; set; } = "#AAEFE8DE";
    public string CalendarPanelHex { get; set; } = "#E6FFFAFA";
    public double PhotoBorderThickness { get; set; } = 2;
    public string PhotoBorderColorHex { get; set; } = "#FFFFFF";
    public double PhotoCornerRadius { get; set; } = 2;

    public bool IsCustom => Style == TemplateStyle.CustomImage
                            || !string.IsNullOrWhiteSpace(BackgroundImagePath);
}

public enum TemplateStyle
{
    SoftOrganic,
    PastelGradient,
    Watercolor,
    Geometric,
    PaperTexture,
    BotanicalAbstract,
    Marble,
    EarthTones,
    PastelWaves,
    Scandinavian,
    Linen,
    Monochrome,
    Aurora,
    CraftPaper,
    SolidWash,
    CustomImage
}
