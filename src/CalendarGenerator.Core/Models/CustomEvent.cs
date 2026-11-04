using System.Text.Json.Serialization;

namespace CalendarGenerator.Core.Models;

public sealed class CustomEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Month 1–12 (year-independent).</summary>
    public int Month { get; set; } = 1;

    /// <summary>Day of month (year-independent; Feb 29 only appears in leap years).</summary>
    public int Day { get; set; } = 1;

    public string Description { get; set; } = string.Empty;

    /// <summary>Legacy project.json field — migrates into Month/Day on load.</summary>
    [JsonPropertyName("date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? LegacyDate
    {
        get => null;
        set
        {
            if (value is not { } d) return;
            Month = d.Month;
            Day = d.Day;
        }
    }

    public void ClampToValidDay()
    {
        Month = Math.Clamp(Month, 1, 12);
        // Allow Feb 29 as a recurring anniversary; other months use a non-leap max.
        var maxDay = Month == 2 ? 29 : DateTime.DaysInMonth(2001, Month);
        Day = Math.Clamp(Day, 1, maxDay);
    }
}
