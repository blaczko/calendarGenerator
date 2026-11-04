using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.NameDays;

namespace CalendarGenerator.Tests;

public class CalendarEngineTests
{
    private readonly CalendarEngine _engine = new(new HungarianCatholicNameDayProvider());

    [Theory]
    [InlineData(2024, 2, 29)] // leap year
    [InlineData(2025, 2, 28)]
    [InlineData(2028, 2, 29)]
    public void DaysInMonth_AreCorrect(int year, int month, int expectedLastDay)
    {
        var project = new CalendarProject { Year = year };
        var page = _engine.BuildMonth(project, month);
        var days = page.Cells.Where(c => c.Date is not null).Select(c => c.Date!.Value.Day).ToList();
        Assert.Equal(expectedLastDay, days.Max());
        Assert.Equal(expectedLastDay, days.Count);
    }

    [Fact]
    public void WeekStartsOnMonday()
    {
        // 2027-03-01 is Monday
        var project = new CalendarProject { Year = 2027 };
        var page = _engine.BuildMonth(project, 3);
        Assert.Equal("H", page.WeekdayHeaders[0]);
        Assert.Equal(1, page.Cells[0].DayNumber);
        Assert.Equal(DayOfWeek.Monday, page.Cells[0].Date!.Value.DayOfWeek);
    }

    [Fact]
    public void HungarianMonthNames_AreUsed()
    {
        var project = new CalendarProject { Year = 2027, Language = "hu" };
        Assert.Equal("Május", _engine.BuildMonth(project, 5).MonthName);
        Assert.Equal("December", _engine.BuildMonth(project, 12).MonthName);
    }

    [Fact]
    public void BuildsAllTwelveMonths()
    {
        var project = new CalendarProject { Year = 2027 };
        var year = _engine.BuildYear(project);
        Assert.Equal(12, year.Count);
        Assert.All(year, m => Assert.True(m.Cells.Count is 28 or 35 or 42));
    }

    [Fact]
    public void MultipleEvents_AppearOnSameDay()
    {
        var project = new CalendarProject { Year = 2027 };
        project.Events.Add(new CustomEvent { Date = new DateOnly(2027, 5, 15), Description = "Boti születésnapja" });
        project.Events.Add(new CustomEvent { Date = new DateOnly(2027, 5, 15), Description = "Családi vacsora" });

        var page = _engine.BuildMonth(project, 5);
        var cell = page.Cells.Single(c => c.DayNumber == 15);
        Assert.Equal(2, cell.Events.Count);
        Assert.True(cell.HasEvents);
    }
}
