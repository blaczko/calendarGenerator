using System.ComponentModel;
using System.Runtime.CompilerServices;
using CalendarGenerator.Core.Templates;

namespace CalendarGenerator.Core.Models;

public sealed class CalendarProject : INotifyPropertyChanged
{
    private string _name = "Új naptár";
    private int _year = DateTime.Now.Year + 1;
    private string _language = "hu";
    private string _nameDaySourceId = "hu-catholic";

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public int Year
    {
        get => _year;
        set => SetField(ref _year, value);
    }

    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public string NameDaySourceId
    {
        get => _nameDaySourceId;
        set => SetField(ref _nameDaySourceId, value);
    }

    public PaperSize Paper { get; set; } = new();
    public List<MonthPhotos> Months { get; set; } = CreateDefaultMonths();
    public List<CustomEvent> Events { get; set; } = new();
    public List<TemplateDefinition> CustomTemplates { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.Now;
    public string? FilePath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static List<MonthPhotos> CreateDefaultMonths()
    {
        var templateIds = new[]
        {
            "soft-beige", "pastel-gradient", "watercolor", "geometric",
            "paper-texture", "botanical-abstract", "marble", "earth-tones",
            "pastel-waves", "scandinavian", "linen", "monochrome"
        };

        return Enumerable.Range(1, 12)
            .Select(m => new MonthPhotos
            {
                Month = m,
                TemplateId = templateIds[(m - 1) % templateIds.Length]
            })
            .ToList();
    }

    public MonthPhotos GetMonth(int month) =>
        Months.First(m => m.Month == month);

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
