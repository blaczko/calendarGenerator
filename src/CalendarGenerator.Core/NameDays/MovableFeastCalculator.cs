namespace CalendarGenerator.Core.NameDays;

/// <summary>
/// Computes movable Christian feasts using the Anonymous Gregorian algorithm (Computus).
/// </summary>
public static class MovableFeastCalculator
{
    public static DateOnly EasterSunday(int year)
    {
        // Meeus/Jones/Butcher Gregorian algorithm
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    public static IReadOnlyDictionary<DateOnly, string> GetMovableFeasts(int year, string language)
    {
        var easter = EasterSunday(year);
        var feasts = new Dictionary<DateOnly, string>
        {
            [easter.AddDays(-46)] = language == "hu" ? "Hamvazószerda" : "Ash Wednesday",
            [easter.AddDays(-7)] = language == "hu" ? "Virágvasárnap" : "Palm Sunday",
            [easter.AddDays(-2)] = language == "hu" ? "Nagypéntek" : "Good Friday",
            [easter] = language == "hu" ? "Húsvétvasárnap" : "Easter Sunday",
            [easter.AddDays(1)] = language == "hu" ? "Húsvéthétfő" : "Easter Monday",
            [easter.AddDays(39)] = language == "hu" ? "Mennybemenetel" : "Ascension",
            [easter.AddDays(49)] = language == "hu" ? "Pünkösdvasárnap" : "Pentecost",
            [easter.AddDays(50)] = language == "hu" ? "Pünkösdhétfő" : "Whit Monday",
            [easter.AddDays(60)] = language == "hu" ? "Úrnapja" : "Corpus Christi"
        };
        return feasts;
    }
}
