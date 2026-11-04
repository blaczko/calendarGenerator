namespace CalendarGenerator.Core.NameDays;

public interface INameDayProvider
{
    string SourceId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> GetNameDays(DateOnly date, string language);
}
