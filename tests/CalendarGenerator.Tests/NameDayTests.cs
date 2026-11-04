using CalendarGenerator.Core.NameDays;

namespace CalendarGenerator.Tests;

public class NameDayTests
{
    private readonly HungarianCatholicNameDayProvider _provider = new();

    [Fact]
    public void Christmas_HasExpectedNames()
    {
        var names = _provider.GetNameDays(new DateOnly(2027, 12, 24), "hu");
        Assert.Contains("Ádám", names);
        Assert.Contains("Éva", names);
    }

    [Fact]
    public void MultipleNameDays_Supported()
    {
        var names = _provider.GetNameDays(new DateOnly(2027, 6, 29), "hu");
        Assert.True(names.Count >= 2);
        Assert.Contains("Péter", names);
        Assert.Contains("Pál", names);
    }

    [Theory]
    [InlineData(2024, 3, 31)] // Easter 2024
    [InlineData(2025, 4, 20)]
    [InlineData(2027, 3, 28)]
    public void EasterSunday_MatchesKnownDates(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), MovableFeastCalculator.EasterSunday(year));
    }

    [Fact]
    public void MovableFeast_MergedIntoNameDays()
    {
        var easter = MovableFeastCalculator.EasterSunday(2027);
        var names = _provider.GetNameDays(easter, "hu");
        Assert.Contains("Húsvétvasárnap", names);
    }

    [Fact]
    public void LeapDay_HasName()
    {
        var names = _provider.GetNameDays(new DateOnly(2024, 2, 29), "hu");
        Assert.NotEmpty(names);
    }
}
