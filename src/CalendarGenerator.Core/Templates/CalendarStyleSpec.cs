namespace CalendarGenerator.Core.Templates;

/// <summary>
/// Resolved visual parameters for rendering a calendar grid from format + theme overrides.
/// </summary>
public sealed class CalendarStyleSpec
{
    public double CellGap { get; init; }
    public double CellCornerRadius { get; init; }
    public double PanelCornerRadius { get; init; }
    public double PanelPadding { get; init; }
    public double PanelBorderThickness { get; init; }
    public bool ShowInnerGrid { get; init; } = true;
    public bool ShowWeekendHighlight { get; init; } = true;
    public bool ShowNameDays { get; init; } = true;
    public bool ShowPanelFill { get; init; } = true;
    public bool RuledLines { get; init; }
    public bool DotSeparators { get; init; }
    public bool CircularDayBadge { get; init; }
    public bool BoldDays { get; init; }
    public bool StrongHeader { get; init; }
    public bool TransparentHeader { get; init; }
    public double DaySizeFactor { get; init; } = 1.0;
    public TitleAlignmentMode TitleAlignment { get; init; } = TitleAlignmentMode.Center;
    public DayAlignMode DayAlign { get; init; } = DayAlignMode.Left;
    public HeaderStyleMode HeaderStyle { get; init; } = HeaderStyleMode.Default;

    public static CalendarStyleSpec From(PageTheme theme)
    {
        var baseSpec = FromFormat(theme.CalendarFormat);
        return new CalendarStyleSpec
        {
            CellGap = theme.CellGap ?? baseSpec.CellGap,
            CellCornerRadius = theme.CellCornerRadius ?? baseSpec.CellCornerRadius,
            PanelCornerRadius = theme.PanelCornerRadius ?? baseSpec.PanelCornerRadius,
            PanelPadding = baseSpec.PanelPadding,
            PanelBorderThickness = theme.ShowOuterFrame == false ? 0 : baseSpec.PanelBorderThickness,
            ShowInnerGrid = theme.ShowInnerGrid ?? baseSpec.ShowInnerGrid,
            ShowWeekendHighlight = theme.ShowWeekendHighlight ?? baseSpec.ShowWeekendHighlight,
            ShowNameDays = theme.ShowNameDays ?? baseSpec.ShowNameDays,
            ShowPanelFill = theme.ShowCalendarPanel ?? baseSpec.ShowPanelFill,
            RuledLines = baseSpec.RuledLines,
            DotSeparators = baseSpec.DotSeparators,
            CircularDayBadge = baseSpec.CircularDayBadge,
            BoldDays = baseSpec.BoldDays,
            StrongHeader = baseSpec.StrongHeader,
            TransparentHeader = baseSpec.TransparentHeader,
            DaySizeFactor = baseSpec.DaySizeFactor,
            TitleAlignment = theme.TitleAlignment ?? baseSpec.TitleAlignment,
            DayAlign = theme.DayAlign ?? baseSpec.DayAlign,
            HeaderStyle = theme.HeaderStyle ?? baseSpec.HeaderStyle
        };
    }

    public static CalendarStyleSpec FromFormat(CalendarFormat format) => format switch
    {
        CalendarFormat.Elegant => new CalendarStyleSpec
        {
            CellGap = 0,
            CellCornerRadius = 0,
            PanelCornerRadius = 10,
            PanelPadding = 4,
            PanelBorderThickness = 0.8,
            ShowInnerGrid = true,
            ShowWeekendHighlight = true,
            TitleAlignment = TitleAlignmentMode.Center,
            DayAlign = DayAlignMode.Left,
            HeaderStyle = HeaderStyleMode.Underline,
            TransparentHeader = true,
            DaySizeFactor = 1.0
        },
        CalendarFormat.ModernBold => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 4,
            PanelPadding = 0,
            PanelBorderThickness = 0,
            ShowInnerGrid = true,
            ShowWeekendHighlight = false,
            BoldDays = true,
            StrongHeader = true,
            HeaderStyle = HeaderStyleMode.SolidBar,
            DaySizeFactor = 1.1,
            DayAlign = DayAlignMode.Left
        },
        CalendarFormat.Minimal => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 0,
            PanelPadding = 0,
            PanelBorderThickness = 0,
            ShowInnerGrid = false,
            ShowWeekendHighlight = false,
            ShowPanelFill = false,
            TransparentHeader = true,
            HeaderStyle = HeaderStyleMode.None,
            DayAlign = DayAlignMode.Left,
            DaySizeFactor = 1.05
        },
        CalendarFormat.SoftCards => new CalendarStyleSpec
        {
            CellGap = 3,
            CellCornerRadius = 8,
            PanelCornerRadius = 12,
            PanelPadding = 4,
            PanelBorderThickness = 0,
            ShowInnerGrid = false,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.Pills,
            DayAlign = DayAlignMode.Left
        },
        CalendarFormat.Compact => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 4,
            PanelPadding = 0,
            PanelBorderThickness = 1,
            ShowInnerGrid = true,
            BoldDays = true,
            DaySizeFactor = 0.9,
            HeaderStyle = HeaderStyleMode.Default
        },
        CalendarFormat.Notebook => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 2,
            PanelPadding = 2,
            PanelBorderThickness = 1,
            ShowInnerGrid = true,
            RuledLines = true,
            HeaderStyle = HeaderStyleMode.Underline,
            TransparentHeader = true
        },
        CalendarFormat.Magazine => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 0,
            PanelPadding = 0,
            PanelBorderThickness = 0,
            ShowInnerGrid = true,
            ShowWeekendHighlight = false,
            ShowPanelFill = false,
            TitleAlignment = TitleAlignmentMode.Left,
            HeaderStyle = HeaderStyleMode.Underline,
            TransparentHeader = true,
            DaySizeFactor = 1.05
        },
        CalendarFormat.DotGrid => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 6,
            PanelPadding = 2,
            PanelBorderThickness = 0,
            ShowInnerGrid = false,
            DotSeparators = true,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.None,
            TransparentHeader = true
        },
        CalendarFormat.Banner => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 8,
            PanelPadding = 0,
            PanelBorderThickness = 0,
            ShowInnerGrid = true,
            StrongHeader = true,
            HeaderStyle = HeaderStyleMode.SolidBar,
            DayAlign = DayAlignMode.Center,
            DaySizeFactor = 1.0
        },
        CalendarFormat.OutlineFrame => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 0,
            PanelPadding = 6,
            PanelBorderThickness = 2.5,
            ShowInnerGrid = false,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.Underline,
            TransparentHeader = true
        },
        CalendarFormat.CircularDays => new CalendarStyleSpec
        {
            CellGap = 2,
            CellCornerRadius = 10,
            PanelCornerRadius = 10,
            PanelPadding = 3,
            PanelBorderThickness = 0,
            ShowInnerGrid = false,
            CircularDayBadge = true,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.Pills,
            DayAlign = DayAlignMode.Center,
            DaySizeFactor = 0.95
        },
        CalendarFormat.Poster => new CalendarStyleSpec
        {
            CellGap = 1,
            CellCornerRadius = 0,
            PanelCornerRadius = 0,
            PanelPadding = 0,
            PanelBorderThickness = 0,
            ShowInnerGrid = false,
            ShowPanelFill = false,
            ShowWeekendHighlight = false,
            BoldDays = true,
            DaySizeFactor = 1.45,
            DayAlign = DayAlignMode.Center,
            HeaderStyle = HeaderStyleMode.None,
            TransparentHeader = true,
            TitleAlignment = TitleAlignmentMode.Center
        },
        CalendarFormat.Folio => new CalendarStyleSpec
        {
            CellGap = 0,
            PanelCornerRadius = 0,
            PanelPadding = 4,
            PanelBorderThickness = 2,
            ShowInnerGrid = true,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.SolidBar,
            DayAlign = DayAlignMode.Left,
            DaySizeFactor = 1.0
        },
        _ => new CalendarStyleSpec
        {
            CellGap = 0,
            CellCornerRadius = 0,
            PanelCornerRadius = 6,
            PanelPadding = 1,
            PanelBorderThickness = 1,
            ShowInnerGrid = true,
            ShowWeekendHighlight = true,
            HeaderStyle = HeaderStyleMode.Default,
            DayAlign = DayAlignMode.Left
        }
    };
}
