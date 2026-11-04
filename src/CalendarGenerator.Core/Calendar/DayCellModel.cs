namespace CalendarGenerator.Core.Calendar;

public sealed class DayCellModel
{
    public DateOnly? Date { get; init; }
    public int? DayNumber => Date?.Day;
    public bool IsEmpty => Date is null;
    public bool IsWeekend => Date is { } d && (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    public IReadOnlyList<string> NameDays { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Events { get; init; } = Array.Empty<string>();
    public bool HasEvents => Events.Count > 0;
}
