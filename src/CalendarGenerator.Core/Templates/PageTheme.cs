using System.IO;
using System.Text.Json.Serialization;

namespace CalendarGenerator.Core.Templates;

/// <summary>
/// Per-month editable appearance: background, calendar format, typography and colors.
/// </summary>
public sealed class PageTheme
{
    public string PresetId { get; set; } = "soft-beige";
    public string DisplayName { get; set; } = "Puha bézs";

    public TemplateStyle BackgroundStyle { get; set; } = TemplateStyle.SoftOrganic;
    public string PrimaryHex { get; set; } = "#F3E9DA";
    public string SecondaryHex { get; set; } = "#E0CDB4";
    public string AccentHex { get; set; } = "#C9A88A";
    public string? BackgroundImagePath { get; set; }
    public string? ProjectCopyPath { get; set; }

    public CalendarFormat CalendarFormat { get; set; } = CalendarFormat.Classic;

    // Optional layout overrides (null = use format default)
    public bool? ShowWeekendHighlight { get; set; }
    public bool? ShowNameDays { get; set; }
    public bool? ShowCalendarPanel { get; set; }
    public bool? ShowInnerGrid { get; set; }
    public bool? ShowOuterFrame { get; set; }
    public double? CellGap { get; set; }
    public double? CellCornerRadius { get; set; }
    public double? PanelCornerRadius { get; set; }
    public TitleAlignmentMode? TitleAlignment { get; set; }
    public DayAlignMode? DayAlign { get; set; }
    public HeaderStyleMode? HeaderStyle { get; set; }

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

    [JsonIgnore]
    public bool HasBackgroundImage =>
        (!string.IsNullOrWhiteSpace(ProjectCopyPath) && File.Exists(ProjectCopyPath))
        || (!string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath));

    public PageTheme Clone() => new()
    {
        PresetId = PresetId,
        DisplayName = DisplayName,
        BackgroundStyle = BackgroundStyle,
        PrimaryHex = PrimaryHex,
        SecondaryHex = SecondaryHex,
        AccentHex = AccentHex,
        BackgroundImagePath = BackgroundImagePath,
        ProjectCopyPath = ProjectCopyPath,
        CalendarFormat = CalendarFormat,
        ShowWeekendHighlight = ShowWeekendHighlight,
        ShowNameDays = ShowNameDays,
        ShowCalendarPanel = ShowCalendarPanel,
        ShowInnerGrid = ShowInnerGrid,
        ShowOuterFrame = ShowOuterFrame,
        CellGap = CellGap,
        CellCornerRadius = CellCornerRadius,
        PanelCornerRadius = PanelCornerRadius,
        TitleAlignment = TitleAlignment,
        DayAlign = DayAlign,
        HeaderStyle = HeaderStyle,
        FontFamily = FontFamily,
        TitleFontSize = TitleFontSize,
        YearFontSize = YearFontSize,
        DayFontSize = DayFontSize,
        WeekdayFontSize = WeekdayFontSize,
        NameDayFontSize = NameDayFontSize,
        TitleColorHex = TitleColorHex,
        YearColorHex = YearColorHex,
        DayColorHex = DayColorHex,
        WeekdayColorHex = WeekdayColorHex,
        NameDayColorHex = NameDayColorHex,
        EventColorHex = EventColorHex,
        GridLineHex = GridLineHex,
        WeekendFillHex = WeekendFillHex,
        HeaderFillHex = HeaderFillHex,
        CalendarPanelHex = CalendarPanelHex
    };

    public static PageTheme FromTemplate(TemplateDefinition template) => new()
    {
        PresetId = template.Id,
        DisplayName = template.DisplayName,
        BackgroundStyle = template.Style,
        PrimaryHex = template.PrimaryHex,
        SecondaryHex = template.SecondaryHex,
        AccentHex = template.AccentHex,
        BackgroundImagePath = template.BackgroundImagePath,
        ProjectCopyPath = template.ProjectCopyPath,
        CalendarFormat = template.CalendarFormat,
        FontFamily = template.FontFamily,
        TitleFontSize = template.TitleFontSize,
        YearFontSize = template.YearFontSize,
        DayFontSize = template.DayFontSize,
        WeekdayFontSize = template.WeekdayFontSize,
        NameDayFontSize = template.NameDayFontSize,
        TitleColorHex = template.TitleColorHex,
        YearColorHex = template.YearColorHex,
        DayColorHex = template.DayColorHex,
        WeekdayColorHex = template.WeekdayColorHex,
        NameDayColorHex = template.NameDayColorHex,
        EventColorHex = template.EventColorHex,
        GridLineHex = template.GridLineHex,
        WeekendFillHex = template.WeekendFillHex,
        HeaderFillHex = template.HeaderFillHex,
        CalendarPanelHex = template.CalendarPanelHex
    };

    public TemplateDefinition AsBackgroundTemplate() => new()
    {
        Id = PresetId,
        DisplayName = DisplayName,
        Description = DisplayName,
        PrimaryHex = PrimaryHex,
        SecondaryHex = SecondaryHex,
        AccentHex = AccentHex,
        Style = HasBackgroundImage ? TemplateStyle.CustomImage : BackgroundStyle,
        BackgroundImagePath = BackgroundImagePath,
        ProjectCopyPath = ProjectCopyPath
    };
}
