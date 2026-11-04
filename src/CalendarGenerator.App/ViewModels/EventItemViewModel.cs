using CalendarGenerator.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalendarGenerator.App.ViewModels;

public partial class EventItemViewModel : ObservableObject
{
    private static readonly string[] HungarianMonths =
    [
        "január", "február", "március", "április", "május", "június",
        "július", "augusztus", "szeptember", "október", "november", "december"
    ];

    public CustomEvent Model { get; }

    [ObservableProperty] private int _month;
    [ObservableProperty] private int _day;
    [ObservableProperty] private string _description = string.Empty;

    public EventItemViewModel(CustomEvent model)
    {
        Model = model;
        model.ClampToValidDay();
        _month = model.Month;
        _day = model.Day;
        _description = model.Description;
    }

    partial void OnMonthChanged(int value)
    {
        Model.Month = value;
        Model.ClampToValidDay();
        if (_day != Model.Day)
            Day = Model.Day;
        OnPropertyChanged(nameof(DisplayDate));
    }

    partial void OnDayChanged(int value)
    {
        Model.Day = value;
        Model.ClampToValidDay();
        if (_day != Model.Day)
            _day = Model.Day;
        OnPropertyChanged(nameof(DisplayDate));
    }

    partial void OnDescriptionChanged(string value) => Model.Description = value ?? string.Empty;

    public string DisplayDate
    {
        get
        {
            var name = Month is >= 1 and <= 12 ? HungarianMonths[Month - 1] : "?";
            return $"{Month:00}. {name} {Day}.";
        }
    }
}
