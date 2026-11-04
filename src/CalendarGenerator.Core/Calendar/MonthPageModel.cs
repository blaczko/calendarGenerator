namespace CalendarGenerator.Core.Calendar;

public sealed class MonthPageModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public IReadOnlyList<string> WeekdayHeaders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DayCellModel> Cells { get; init; } = Array.Empty<DayCellModel>();
    public int WeekRows { get; init; }
}
