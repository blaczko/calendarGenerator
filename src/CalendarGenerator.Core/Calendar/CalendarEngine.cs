using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.NameDays;

namespace CalendarGenerator.Core.Calendar;

public sealed class CalendarEngine
{
    private static readonly string[] HungarianMonths =
    [
        "Január", "Február", "Március", "Április", "Május", "Június",
        "Július", "Augusztus", "Szeptember", "Október", "November", "December"
    ];

    private static readonly string[] HungarianWeekdays =
        ["Hétfő", "Kedd", "Szerda", "Csütörtök", "Péntek", "Szombat", "Vasárnap"];

    private static readonly string[] HungarianWeekdaysShort =
        ["H", "K", "Sze", "Cs", "P", "Szo", "V"];

    private readonly INameDayProvider _nameDayProvider;

    public CalendarEngine(INameDayProvider nameDayProvider)
    {
        _nameDayProvider = nameDayProvider;
    }

    public string GetMonthName(int month, string language = "hu") =>
        language switch
        {
            "hu" => HungarianMonths[month - 1],
            _ => new DateTime(2000, month, 1).ToString("MMMM")
        };

    public IReadOnlyList<string> GetWeekdayHeaders(string language = "hu", bool shortNames = true) =>
        language switch
        {
            "hu" => shortNames ? HungarianWeekdaysShort : HungarianWeekdays,
            _ => shortNames
                ? ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
                : ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
        };

    public MonthPageModel BuildMonth(CalendarProject project, int month)
    {
        var year = project.Year;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var first = new DateOnly(year, month, 1);

        // Monday = 0 ... Sunday = 6
        var offset = ((int)first.DayOfWeek + 6) % 7;
        var totalCells = offset + daysInMonth;
        var weekRows = (int)Math.Ceiling(totalCells / 7.0);
        var cellCount = weekRows * 7;

        var daysInThisMonth = daysInMonth;
        var eventsByDay = project.Events
            .Where(e => e.Month == month && e.Day >= 1 && e.Day <= daysInThisMonth)
            .GroupBy(e => e.Day)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Description).ToList());

        var cells = new List<DayCellModel>(cellCount);
        for (var i = 0; i < cellCount; i++)
        {
            var dayIndex = i - offset + 1;
            if (dayIndex < 1 || dayIndex > daysInMonth)
            {
                cells.Add(new DayCellModel());
                continue;
            }

            var date = new DateOnly(year, month, dayIndex);
            eventsByDay.TryGetValue(dayIndex, out var dayEvents);

            cells.Add(new DayCellModel
            {
                Date = date,
                NameDays = _nameDayProvider.GetNameDays(date, project.Language),
                Events = dayEvents ?? Array.Empty<string>()
            });
        }

        return new MonthPageModel
        {
            Year = year,
            Month = month,
            MonthName = GetMonthName(month, project.Language),
            WeekdayHeaders = GetWeekdayHeaders(project.Language),
            Cells = cells,
            WeekRows = weekRows
        };
    }

    public IReadOnlyList<MonthPageModel> BuildYear(CalendarProject project) =>
        Enumerable.Range(1, 12).Select(m => BuildMonth(project, m)).ToList();
}
