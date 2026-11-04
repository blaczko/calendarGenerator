using System.IO;
using CalendarGenerator.Core.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalendarGenerator.App.ViewModels;

/// <summary>
/// Observable wrapper around a month's <see cref="PageTheme"/> for the Templates UI.
/// </summary>
public partial class ThemeEditorViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private PageTheme _theme;

    public ThemeEditorViewModel(PageTheme theme, Action onChanged)
    {
        _theme = theme;
        _onChanged = onChanged;
    }

    public void ReplaceTheme(PageTheme theme)
    {
        _theme = theme;
        OnPropertyChanged(string.Empty);
    }

    public PageTheme Model => _theme;

    public CalendarFormat CalendarFormat
    {
        get => _theme.CalendarFormat;
        set => ApplyCalendarFormat(value);
    }

    public void ApplyCalendarFormat(CalendarFormat value)
    {
        if (_theme.CalendarFormat == value)
        {
            // Still clear stale checkbox overrides so format visuals can show.
            ClearLayoutOverrides();
            NotifyLayoutProps();
            _onChanged();
            return;
        }

        _theme.CalendarFormat = value;
        ClearLayoutOverrides();
        OnPropertyChanged(nameof(CalendarFormat));
        NotifyLayoutProps();
        _onChanged();
    }

    private void ClearLayoutOverrides()
    {
        _theme.ShowWeekendHighlight = null;
        _theme.ShowNameDays = null;
        _theme.ShowCalendarPanel = null;
        _theme.ShowInnerGrid = null;
        _theme.ShowOuterFrame = null;
        _theme.CellGap = null;
        _theme.CellCornerRadius = null;
        _theme.PanelCornerRadius = null;
        _theme.TitleAlignment = null;
        _theme.DayAlign = null;
        _theme.HeaderStyle = null;
    }

    private void NotifyLayoutProps()
    {
        OnPropertyChanged(nameof(ShowWeekendHighlight));
        OnPropertyChanged(nameof(ShowNameDays));
        OnPropertyChanged(nameof(ShowCalendarPanel));
        OnPropertyChanged(nameof(ShowInnerGrid));
        OnPropertyChanged(nameof(CenterDayNumbers));
        OnPropertyChanged(nameof(LeftTitle));
    }

    private CalendarStyleSpec Spec => CalendarStyleSpec.From(_theme);
    private CalendarStyleSpec FormatDefaults => CalendarStyleSpec.FromFormat(_theme.CalendarFormat);

    public bool ShowWeekendHighlight
    {
        get => Spec.ShowWeekendHighlight;
        set
        {
            _theme.ShowWeekendHighlight = value == FormatDefaults.ShowWeekendHighlight ? null : value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool ShowNameDays
    {
        get => Spec.ShowNameDays;
        set
        {
            _theme.ShowNameDays = value == FormatDefaults.ShowNameDays ? null : value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool ShowCalendarPanel
    {
        get => Spec.ShowPanelFill;
        set
        {
            _theme.ShowCalendarPanel = value == FormatDefaults.ShowPanelFill ? null : value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool ShowInnerGrid
    {
        get => Spec.ShowInnerGrid;
        set
        {
            _theme.ShowInnerGrid = value == FormatDefaults.ShowInnerGrid ? null : value;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool CenterDayNumbers
    {
        get => Spec.DayAlign == DayAlignMode.Center;
        set
        {
            var desired = value ? DayAlignMode.Center : DayAlignMode.Left;
            _theme.DayAlign = desired == FormatDefaults.DayAlign ? null : desired;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public bool LeftTitle
    {
        get => Spec.TitleAlignment == TitleAlignmentMode.Left;
        set
        {
            var desired = value ? TitleAlignmentMode.Left : TitleAlignmentMode.Center;
            _theme.TitleAlignment = desired == FormatDefaults.TitleAlignment ? null : desired;
            OnPropertyChanged();
            _onChanged();
        }
    }

    public string FontFamily
    {
        get => _theme.FontFamily;
        set { if (_theme.FontFamily == value) return; _theme.FontFamily = value ?? "Georgia"; OnPropertyChanged(); _onChanged(); }
    }

    public double TitleFontSize
    {
        get => _theme.TitleFontSize;
        set { var v = Math.Clamp(value, 18, 56); if (Math.Abs(_theme.TitleFontSize - v) < 0.01) return; _theme.TitleFontSize = v; OnPropertyChanged(); _onChanged(); }
    }

    public double DayFontSize
    {
        get => _theme.DayFontSize;
        set { var v = Math.Clamp(value, 10, 36); if (Math.Abs(_theme.DayFontSize - v) < 0.01) return; _theme.DayFontSize = v; OnPropertyChanged(); _onChanged(); }
    }

    public double NameDayFontSize
    {
        get => _theme.NameDayFontSize;
        set { var v = Math.Clamp(value, 6, 16); if (Math.Abs(_theme.NameDayFontSize - v) < 0.01) return; _theme.NameDayFontSize = v; OnPropertyChanged(); _onChanged(); }
    }

    public string TitleColorHex
    {
        get => _theme.TitleColorHex;
        set { if (_theme.TitleColorHex == value) return; _theme.TitleColorHex = value ?? "#2C2C2C"; OnPropertyChanged(); _onChanged(); }
    }

    public string DayColorHex
    {
        get => _theme.DayColorHex;
        set { if (_theme.DayColorHex == value) return; _theme.DayColorHex = value ?? "#222222"; OnPropertyChanged(); _onChanged(); }
    }

    public string NameDayColorHex
    {
        get => _theme.NameDayColorHex;
        set { if (_theme.NameDayColorHex == value) return; _theme.NameDayColorHex = value ?? "#5A554C"; OnPropertyChanged(); _onChanged(); }
    }

    public string EventColorHex
    {
        get => _theme.EventColorHex;
        set { if (_theme.EventColorHex == value) return; _theme.EventColorHex = value ?? "#C62828"; OnPropertyChanged(); _onChanged(); }
    }

    public string PrimaryHex
    {
        get => _theme.PrimaryHex;
        set { if (_theme.PrimaryHex == value) return; _theme.PrimaryHex = value ?? "#F3E9DA"; OnPropertyChanged(); _onChanged(); }
    }

    public string SecondaryHex
    {
        get => _theme.SecondaryHex;
        set { if (_theme.SecondaryHex == value) return; _theme.SecondaryHex = value ?? "#E0CDB4"; OnPropertyChanged(); _onChanged(); }
    }

    public string AccentHex
    {
        get => _theme.AccentHex;
        set { if (_theme.AccentHex == value) return; _theme.AccentHex = value ?? "#C9A88A"; OnPropertyChanged(); _onChanged(); }
    }

    public string? BackgroundImagePath =>
        !string.IsNullOrWhiteSpace(_theme.ProjectCopyPath) ? _theme.ProjectCopyPath : _theme.BackgroundImagePath;

    public bool HasBackgroundImage => _theme.HasBackgroundImage;

    public string BackgroundSummary =>
        HasBackgroundImage
            ? Path.GetFileName(BackgroundImagePath) ?? "Egyedi kép"
            : _theme.DisplayName;

    public void NotifyBackgroundChanged()
    {
        OnPropertyChanged(nameof(BackgroundImagePath));
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(BackgroundSummary));
        _onChanged();
    }
}
