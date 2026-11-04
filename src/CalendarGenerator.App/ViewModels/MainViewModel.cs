using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CalendarGenerator.App.Controls;
using CalendarGenerator.App.Services;
using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.NameDays;
using CalendarGenerator.Core.Photos;
using CalendarGenerator.Core.Storage;
using CalendarGenerator.Core.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CalendarGenerator.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ProjectStore _store = new();
    private readonly TemplateLibrary _templates = new();
    private readonly NameDayRegistry _nameDays;
    private readonly CalendarEngine _calendarEngine;
    private readonly PageRenderService _pageRender = new();
    private readonly PdfExportService _pdfExport;
    private CancellationTokenSource? _autoSaveCts;

    public event EventHandler? PhotosChanged;

    public MainViewModel()
    {
        _nameDays = new NameDayRegistry([new HungarianCatholicNameDayProvider()]);
        _calendarEngine = new CalendarEngine(_nameDays.Get("hu-catholic"));
        _pdfExport = new PdfExportService(_pageRender, _calendarEngine);

        Project = new CalendarProject();
        Years = Enumerable.Range(DateTime.Now.Year - 1, 12).ToList();
        NameDaySources = _nameDays.All.ToList();
        Templates = new ObservableCollection<TemplateDefinition>();
        MonthOptions = Enumerable.Range(1, 12)
            .Select(m => new MonthOption(m, _calendarEngine.GetMonthName(m)))
            .ToList();

        ThemeEditor = new ThemeEditorViewModel(
            Project.GetMonth(1).GetOrCreateTheme(_templates, Project.CustomTemplates),
            OnThemeEdited);

        RefreshTemplates();
        RefreshEvents();
        RefreshPhotos();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    public IReadOnlyList<int> Years { get; }
    public IReadOnlyList<INameDayProvider> NameDaySources { get; }
    public IReadOnlyList<MonthOption> MonthOptions { get; }
    public ObservableCollection<TemplateDefinition> Templates { get; }
    public ObservableCollection<EventItemViewModel> Events { get; } = new();
    public ObservableCollection<PhotoThumbViewModel> CurrentMonthPhotos { get; } = new();
    public ThemeEditorViewModel ThemeEditor { get; }

    public IReadOnlyList<FormatOption> CalendarFormats { get; } =
        CalendarFormatInfo.All.Select(x => new FormatOption(x.Format, x.Name, x.Description)).ToList();

    public FormatOption? SelectedCalendarFormat
    {
        get => CalendarFormats.FirstOrDefault(f => f.Format == ThemeEditor.CalendarFormat);
        set
        {
            if (value is null) return;
            if (ThemeEditor.CalendarFormat == value.Format) return;
            ThemeEditor.ApplyCalendarFormat(value.Format);
            OnPropertyChanged();
            StatusMessage = $"Naptár stílus: {value.Name}";
        }
    }

    public IReadOnlyList<string> FontChoices { get; } = TemplateLibrary.FontChoices;

    public IReadOnlyList<ColorOption> ColorChoices { get; } =
    [
        new("#2C2C2C", "Sötét szürke"),
        new("#222222", "Fekete"),
        new("#5A554C", "Meleg szürke"),
        new("#3D2B1F", "Barna"),
        new("#1A3A5C", "Sötétkék"),
        new("#2E5A3C", "Zöld"),
        new("#C62828", "Piros"),
        new("#8B3A2A", "Terrakotta"),
        new("#555555", "Közép szürke"),
        new("#6B5B4F", "Taupe")
    ];

    [ObservableProperty] private CalendarProject _project = null!;
    [ObservableProperty] private NavPage _currentPage = NavPage.Settings;
    [ObservableProperty] private int _selectedMonth = 1;
    [ObservableProperty] private string _statusMessage = "Készen áll.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _exportProgress;
    [ObservableProperty] private BitmapSource? _previewImage;
    [ObservableProperty] private string _newEventDescription = string.Empty;
    [ObservableProperty] private int _newEventMonth = DateTime.Today.Month;
    [ObservableProperty] private int _newEventDay = DateTime.Today.Day;
    [ObservableProperty] private EventItemViewModel? _selectedEvent;
    [ObservableProperty] private PhotoThumbViewModel? _selectedPhoto;
    [ObservableProperty] private string _selectedTemplateId = "soft-beige";
    [ObservableProperty] private PageOrientation _selectedOrientation = PageOrientation.Portrait;
    [ObservableProperty] private string _newTemplateName = string.Empty;
    [ObservableProperty] private string _newTemplateImagePath = string.Empty;
    private bool _suppressTemplateApply;

    public bool CanDeleteSelectedTemplate =>
        Templates.FirstOrDefault(t => t.Id == SelectedTemplateId)?.IsCustom == true;

    public bool IsSettings => CurrentPage == NavPage.Settings;
    public bool IsPhotos => CurrentPage == NavPage.Photos;
    public bool IsEvents => CurrentPage == NavPage.Events;
    public bool IsTemplates => CurrentPage == NavPage.Templates;
    public bool IsPreview => CurrentPage == NavPage.Preview;
    public bool IsExport => CurrentPage == NavPage.Export;

    partial void OnCurrentPageChanged(NavPage value)
    {
        OnPropertyChanged(nameof(IsSettings));
        OnPropertyChanged(nameof(IsPhotos));
        OnPropertyChanged(nameof(IsEvents));
        OnPropertyChanged(nameof(IsTemplates));
        OnPropertyChanged(nameof(IsPreview));
        OnPropertyChanged(nameof(IsExport));

        if (value is NavPage.Preview or NavPage.Export or NavPage.Templates or NavPage.Photos)
            _ = RefreshPreviewAsync();
    }

    partial void OnSelectedMonthChanged(int value)
    {
        RefreshPhotos();
        SyncThemeEditor();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    partial void OnSelectedOrientationChanged(PageOrientation value)
    {
        Project.Paper.Orientation = value;
        Project.Paper.ApplyPreset();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    partial void OnSelectedTemplateIdChanged(string value)
    {
        if (_suppressTemplateApply || string.IsNullOrWhiteSpace(value)) return;

        var month = Project.GetMonth(SelectedMonth);
        var preset = _templates.Resolve(value, Project.CustomTemplates);
        month.TemplateId = value;
        month.Theme = PageTheme.FromTemplate(preset);
        ThemeEditor.ReplaceTheme(month.Theme);
        OnPropertyChanged(nameof(CanDeleteSelectedTemplate));
        OnPropertyChanged(nameof(SelectedCalendarFormat));
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = $"Háttérsablon: {preset.DisplayName}";
    }

    private void SyncThemeEditor()
    {
        var month = Project.GetMonth(SelectedMonth);
        var theme = month.GetOrCreateTheme(_templates, Project.CustomTemplates);
        ThemeEditor.ReplaceTheme(theme);

        _suppressTemplateApply = true;
        SelectedTemplateId = month.TemplateId;
        _suppressTemplateApply = false;
        OnPropertyChanged(nameof(CanDeleteSelectedTemplate));
        OnPropertyChanged(nameof(SelectedCalendarFormat));
    }

    private int _previewGeneration;

    private void OnThemeEdited()
    {
        OnPropertyChanged(nameof(SelectedCalendarFormat));
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void Navigate(string page)
    {
        if (Enum.TryParse<NavPage>(page, out var nav))
            CurrentPage = nav;
    }

    [RelayCommand]
    private void ApplyYear()
    {
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = $"Naptár generálva: {Project.Year}";
    }

    public IReadOnlyList<int> NewEventDayOptions
    {
        get
        {
            var max = NewEventMonth == 2 ? 29 : DateTime.DaysInMonth(2001, Math.Clamp(NewEventMonth, 1, 12));
            return Enumerable.Range(1, max).ToList();
        }
    }

    partial void OnNewEventMonthChanged(int value)
    {
        OnPropertyChanged(nameof(NewEventDayOptions));
        var max = value == 2 ? 29 : DateTime.DaysInMonth(2001, Math.Clamp(value, 1, 12));
        if (NewEventDay > max)
            NewEventDay = max;
    }

    public const int MaxPhotosPerMonth = 12;

    [RelayCommand]
    private async Task AddPhotosAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Fényképek kiválasztása (több is kijelölhető)",
            Filter = "Képek|*.jpg;*.jpeg;*.png;*.webp;*.JPG;*.JPEG;*.PNG;*.WEBP|"
                   + "JPEG|*.jpg;*.jpeg;*.JPG;*.JPEG|"
                   + "PNG|*.png;*.PNG|"
                   + "WebP|*.webp;*.WEBP|"
                   + "Minden fájl|*.*",
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        // FileNames contains every file chosen in one multi-select.
        await ImportPhotosAsync(dialog.FileNames);
    }

    public async Task ImportPhotosAsync(IEnumerable<string> paths)
    {
        var month = Project.GetMonth(SelectedMonth);
        var remaining = MaxPhotosPerMonth - month.Photos.Count;
        if (remaining <= 0)
        {
            StatusMessage = $"Maximum {MaxPhotosPerMonth} fénykép / hónap.";
            return;
        }

        var selected = paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selected.Count == 0)
        {
            StatusMessage = "Nem lett fénykép kiválasztva.";
            return;
        }

        IsBusy = true;
        var added = 0;
        var skipped = 0;
        try
        {
            var cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CatholicCalendarCreator", "imports");

            foreach (var path in selected)
            {
                if (added >= remaining)
                {
                    skipped++;
                    continue;
                }

                if (!File.Exists(path) || !PhotoImportService.IsSupported(path))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var prepared = await PhotoImportService.PrepareAsync(path, cache);
                    month.Photos.Add(new PhotoItem
                    {
                        SourcePath = prepared.Path,
                        AspectRatio = prepared.AspectRatio
                    });
                    added++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    StatusMessage = $"Kép betöltési hiba: {ex.Message}";
                }
            }

            RefreshPhotos();
            await RefreshPreviewAsync();
            ScheduleAutoSave();

            StatusMessage = skipped > 0
                ? $"{added} fénykép hozzáadva ({skipped} kihagyva). Összesen: {month.Photos.Count}/{MaxPhotosPerMonth}."
                : $"{added} fénykép hozzáadva. Összesen: {month.Photos.Count}/{MaxPhotosPerMonth}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveSelectedPhoto()
    {
        if (SelectedPhoto is null) return;
        var month = Project.GetMonth(SelectedMonth);
        month.Photos.Remove(SelectedPhoto.Photo);
        SelectedPhoto = null;
        RefreshPhotos();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void ClearMonthPhotos()
    {
        Project.GetMonth(SelectedMonth).Photos.Clear();
        RefreshPhotos();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void AddEvent()
    {
        if (string.IsNullOrWhiteSpace(NewEventDescription))
        {
            StatusMessage = "Adjon meg eseményleírást.";
            return;
        }

        var evt = new CustomEvent
        {
            Month = NewEventMonth,
            Day = NewEventDay,
            Description = NewEventDescription.Trim()
        };
        evt.ClampToValidDay();
        Project.Events.Add(evt);
        NewEventDescription = string.Empty;
        RefreshEvents();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = "Esemény hozzáadva.";
    }

    [RelayCommand]
    private void DeleteSelectedEvent()
    {
        if (SelectedEvent is null) return;
        Project.Events.Remove(SelectedEvent.Model);
        SelectedEvent = null;
        RefreshEvents();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
    }

    [RelayCommand]
    private void LoadSelectedEvent()
    {
        if (SelectedEvent is null) return;
        NewEventMonth = SelectedEvent.Month;
        NewEventDay = SelectedEvent.Day;
        NewEventDescription = SelectedEvent.Description;
    }

    [RelayCommand]
    private void UpdateSelectedEvent()
    {
        if (SelectedEvent is null)
        {
            StatusMessage = "Válasszon eseményt a módosításhoz.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEventDescription))
        {
            StatusMessage = "Adjon meg eseményleírást.";
            return;
        }

        SelectedEvent.Month = NewEventMonth;
        SelectedEvent.Day = NewEventDay;
        SelectedEvent.Description = NewEventDescription.Trim();
        RefreshEvents();
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = "Esemény módosítva.";
    }

    [RelayCommand]
    private void ResetPhotoLayouts()
    {
        foreach (var photo in Project.GetMonth(SelectedMonth).Photos)
            photo.HasManualLayout = false;

        PhotosChanged?.Invoke(this, EventArgs.Empty);
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = "Automatikus fénykép-elrendezés visszaállítva.";
    }

    public void NotifyPhotoLayoutChanged()
    {
        _ = RefreshPreviewAsync();
        ScheduleAutoSave();
        StatusMessage = "Fénykép elrendezés frissítve.";
    }

    public void BindInteractivePreview(CalendarPageControl control)
    {
        var size = _pageRender.GetPageSizeDip(Project.Paper);
        var month = _calendarEngine.BuildMonth(Project, SelectedMonth);
        var monthPhotos = Project.GetMonth(SelectedMonth);
        var theme = monthPhotos.GetOrCreateTheme(_templates, Project.CustomTemplates);
        var (photoW, photoH) = PageRenderService.GetPhotoAreaSize(size);
        var collage = new CollageEngine().Build(monthPhotos.Photos, photoW / photoH);

        control.Width = size.Width;
        control.Height = size.Height;
        control.Bind(month, monthPhotos, collage, theme, size, interactivePhotos: true);
    }

    [RelayCommand]
    private void ApplyTemplateToAll()
    {
        var source = Project.GetMonth(SelectedMonth).GetOrCreateTheme(_templates, Project.CustomTemplates);
        foreach (var m in Project.Months)
        {
            m.TemplateId = SelectedTemplateId;
            m.Theme = source.Clone();
        }

        StatusMessage = "Megjelenés alkalmazva minden hónapra.";
        ScheduleAutoSave();
        _ = RefreshPreviewAsync();
    }

    [RelayCommand]
    private async Task SetMonthBackgroundImageAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Háttérkép a hónaphoz",
            Filter = "Képek|*.jpg;*.jpeg;*.png;*.webp;*.JPG;*.JPEG;*.PNG;*.WEBP",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CatholicCalendarCreator", "templates");
            Directory.CreateDirectory(cache);
            var prepared = await PhotoImportService.PrepareAsync(dialog.FileName, cache);

            var theme = Project.GetMonth(SelectedMonth).GetOrCreateTheme(_templates, Project.CustomTemplates);
            theme.BackgroundImagePath = prepared.Path;
            theme.ProjectCopyPath = null;
            theme.BackgroundStyle = TemplateStyle.CustomImage;
            ThemeEditor.NotifyBackgroundChanged();
            StatusMessage = "Háttérkép beállítva ehhez a hónaphoz.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Háttérkép hiba: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearMonthBackgroundImage()
    {
        var theme = Project.GetMonth(SelectedMonth).GetOrCreateTheme(_templates, Project.CustomTemplates);
        theme.BackgroundImagePath = null;
        theme.ProjectCopyPath = null;
        if (theme.BackgroundStyle == TemplateStyle.CustomImage)
            theme.BackgroundStyle = TemplateStyle.SoftOrganic;
        ThemeEditor.NotifyBackgroundChanged();
        StatusMessage = "Egyedi háttérkép eltávolítva.";
    }

    [RelayCommand]
    private void BrowseTemplateImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Háttérkép kiválasztása",
            Filter = "Képek|*.jpg;*.jpeg;*.png;*.webp;*.JPG;*.JPEG;*.PNG;*.WEBP",
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
            return;

        NewTemplateImagePath = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NewTemplateName))
            NewTemplateName = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    [RelayCommand]
    private async Task CreateCustomTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTemplateImagePath) || !File.Exists(NewTemplateImagePath))
        {
            StatusMessage = "Válasszon háttérképet az új sablonhoz.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewTemplateName))
        {
            StatusMessage = "Adjon nevet az új sablonnak.";
            return;
        }

        try
        {
            var cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CatholicCalendarCreator", "templates");
            Directory.CreateDirectory(cache);
            var prepared = await PhotoImportService.PrepareAsync(NewTemplateImagePath, cache);

            var template = new TemplateDefinition
            {
                Id = "custom-" + Guid.NewGuid().ToString("N")[..12],
                DisplayName = NewTemplateName.Trim(),
                Description = "Egyedi háttérkép",
                PrimaryHex = "#F5F0E8",
                SecondaryHex = "#E4D9C8",
                AccentHex = "#C4B09A",
                Style = TemplateStyle.CustomImage,
                BackgroundImagePath = prepared.Path,
                CalendarFormat = ThemeEditor.CalendarFormat,
                FontFamily = ThemeEditor.FontFamily
            };

            Project.CustomTemplates.Insert(0, template);
            RefreshTemplates();
            SelectedTemplateId = template.Id;
            NewTemplateName = string.Empty;
            NewTemplateImagePath = string.Empty;
            ScheduleAutoSave();
            StatusMessage = $"Új sablon létrehozva: {template.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sablon hiba: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteCustomTemplate()
    {
        var template = Templates.FirstOrDefault(t => t.Id == SelectedTemplateId);
        if (template is null || !template.IsCustom)
        {
            StatusMessage = "Csak egyedi sablon törölhető.";
            return;
        }

        Project.CustomTemplates.RemoveAll(t => t.Id == template.Id);
        foreach (var month in Project.Months.Where(m => m.TemplateId == template.Id))
        {
            month.TemplateId = "soft-beige";
            month.Theme = null;
        }

        RefreshTemplates();
        SyncThemeEditor();
        ScheduleAutoSave();
        StatusMessage = $"Sablon törölve: {template.DisplayName}";
    }

    private void RefreshTemplates()
    {
        Templates.Clear();
        foreach (var t in _templates.MergeWithCustom(Project.CustomTemplates))
            Templates.Add(t);
        OnPropertyChanged(nameof(CanDeleteSelectedTemplate));
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        var generation = Interlocked.Increment(ref _previewGeneration);
        try
        {
            var monthIndex = SelectedMonth;
            var model = _calendarEngine.BuildMonth(Project, monthIndex);
            // Capture theme snapshot so render always uses the latest style.
            var monthPhotos = Project.GetMonth(monthIndex);
            _ = monthPhotos.GetOrCreateTheme(_templates, Project.CustomTemplates);

            var bitmap = await Application.Current.Dispatcher.InvokeAsync(() =>
                _pageRender.RenderMonth(Project, model, dpi: 120));

            if (generation != _previewGeneration)
                return;

            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            if (generation == _previewGeneration)
                StatusMessage = $"Előnézet hiba: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task PrevMonthAsync()
    {
        SelectedMonth = SelectedMonth <= 1 ? 12 : SelectedMonth - 1;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        SelectedMonth = SelectedMonth >= 12 ? 1 : SelectedMonth + 1;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _store.SaveAsync(Project);
            StatusMessage = $"Projekt mentve: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Mentési hiba: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Projekt megnyitása",
            Filter = "Naptár projekt|project.json",
            InitialDirectory = _store.ProjectsRoot
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var loaded = await _store.LoadAsync(dialog.FileName);
            if (loaded is null)
            {
                StatusMessage = "A projekt nem tölthető be.";
                return;
            }

            Project = loaded;
            SelectedOrientation = Project.Paper.Orientation;
            SelectedMonth = 1;
            RefreshTemplates();
            SyncThemeEditor();
            RefreshEvents();
            RefreshPhotos();
            await RefreshPreviewAsync();
            StatusMessage = $"Betöltve: {Project.Name}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        Project = new CalendarProject();
        SelectedMonth = 1;
        SelectedOrientation = PageOrientation.Portrait;
        RefreshTemplates();
        SyncThemeEditor();
        RefreshEvents();
        RefreshPhotos();
        await RefreshPreviewAsync();
        StatusMessage = "Új projekt létrehozva.";
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "PDF mentése",
            Filter = "PDF|*.pdf",
            FileName = $"Naptar_{Project.Year}.pdf"
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        ExportProgress = 0;
        try
        {
            var progress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
                if (msg.Contains('/'))
                {
                    var parts = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var fraction = parts.FirstOrDefault(p => p.Contains('/'));
                    if (fraction is not null)
                    {
                        var nums = fraction.TrimEnd('…', '.').Split('/');
                        if (nums.Length == 2 && int.TryParse(nums[0], out var a) && int.TryParse(nums[1], out var b) && b > 0)
                            ExportProgress = a * 100.0 / b;
                    }
                }
                else if (msg.Contains("Kész"))
                {
                    ExportProgress = 100;
                }
            });

            await _pdfExport.ExportAsync(Project, dialog.FileName, progress);
            StatusMessage = $"PDF elkészült: {dialog.FileName}";
            MessageBox.Show($"A naptár PDF sikeresen elkészült.\n\n{dialog.FileName}", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF hiba: {ex.Message}";
            MessageBox.Show(ex.Message, "Export hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshEvents()
    {
        Events.Clear();
        foreach (var e in Project.Events.OrderBy(x => x.Month).ThenBy(x => x.Day).ThenBy(x => x.Description))
            Events.Add(new EventItemViewModel(e));
    }

    private void RefreshPhotos()
    {
        CurrentMonthPhotos.Clear();
        foreach (var p in Project.GetMonth(SelectedMonth).Photos)
            CurrentMonthPhotos.Add(new PhotoThumbViewModel(p));
        PhotosChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500, token);
                await _store.SaveAsync(Project, token);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!IsBusy)
                        StatusMessage = "Automatikus mentés kész.";
                });
            }
            catch (OperationCanceledException) { }
            catch
            {
                // ignore background auto-save errors
            }
        }, token);
    }

}

public sealed record MonthOption(int Month, string Name)
{
    public override string ToString() => $"{Month}. {Name}";
}

public sealed record FormatOption(CalendarFormat Format, string Name, string Description)
{
    public override string ToString() => Name;
}

public sealed record ColorOption(string Hex, string Name)
{
    public override string ToString() => Name;
}
