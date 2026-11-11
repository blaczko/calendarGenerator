using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CalendarGenerator.App.Services;
using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Photos;
using CalendarGenerator.Core.Templates;

namespace CalendarGenerator.App.Controls;

public partial class CalendarPageControl : UserControl
{
    private static readonly FontFamily HandFont = new("Segoe Print, Ink Free, Comic Sans MS, Georgia");

    private readonly List<(DayCellModel Cell, int Row, int Col)> _eventCells = new();
    private int _weekRows;
    private PageTheme _theme = new();
    private SolidColorBrush _eventInk = Brushes.IndianRed;
    private SolidColorBrush _dayNumberBrush = Brushes.Black;
    private SolidColorBrush _nameDayBrush = Brushes.DimGray;
    private SolidColorBrush _weekendBg = Brushes.Transparent;
    private SolidColorBrush _headerBg = Brushes.Transparent;
    private SolidColorBrush _cellBorder = Brushes.Gray;

    public CalendarPageControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => FinalizeEventLayout();
    }

    public event EventHandler? PhotoLayoutChanged;

    public void Bind(
        MonthPageModel month,
        MonthPhotos monthPhotos,
        CollageLayout collage,
        TemplateDefinition template,
        Size pageSize,
        bool interactivePhotos = false)
    {
        var theme = monthPhotos.Theme ?? PageTheme.FromTemplate(template);
        Bind(month, monthPhotos, collage, theme, pageSize, interactivePhotos);
    }

    public void Bind(
        MonthPageModel month,
        MonthPhotos monthPhotos,
        CollageLayout collage,
        PageTheme theme,
        Size pageSize,
        bool interactivePhotos = false)
    {
        _theme = theme ?? new PageTheme();
        ApplyThemeBrushes();

        MonthTitle.Text = month.MonthName;
        YearTitle.Text = month.Year.ToString();
        ApplyTitleChrome();

        PageBackground.Background = TemplateBackgroundFactory.Create(_theme, pageSize.Width, pageSize.Height);

        const double assemblyCm = 1.2;
        var assemblyDip = assemblyCm / 2.54 * 96.0;
        AssemblyRow.Height = new GridLength(assemblyDip);
        var contentHeight = Math.Max(200, pageSize.Height - assemblyDip);
        var photoRatio = _theme.CalendarFormat == CalendarFormat.Compact ? 0.44 : 0.40;
        var photoAreaHeight = Math.Max(120, contentHeight * photoRatio);
        PhotoSection.Height = photoAreaHeight;

        InteractivePhotos.LayoutChanged -= OnInteractiveLayoutChanged;
        if (interactivePhotos)
        {
            PhotoCanvas.Visibility = Visibility.Collapsed;
            PhotoCanvas.Children.Clear();
            InteractivePhotos.Visibility = Visibility.Visible;
            InteractivePhotos.SetFrameStyle(
                _theme.PhotoBorderThickness,
                _theme.PhotoBorderColorHex,
                _theme.PhotoCornerRadius);
            InteractivePhotos.SetPhotos(monthPhotos.Photos);
            InteractivePhotos.LayoutChanged += OnInteractiveLayoutChanged;
            EmptyPhotoHint.Visibility = monthPhotos.Photos.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else
        {
            InteractivePhotos.Visibility = Visibility.Collapsed;
            InteractivePhotos.SetPhotos(Array.Empty<PhotoItem>());
            PhotoCanvas.Visibility = Visibility.Visible;
            BuildPhotos(monthPhotos, collage, pageSize.Width - 44, photoAreaHeight);
        }

        BuildCalendarGrid(month);
        FinalizeEventLayout();
    }

    private CalendarStyleSpec _style = CalendarStyleSpec.FromFormat(CalendarFormat.Classic);

    private void ApplyThemeBrushes()
    {
        _style = CalendarStyleSpec.From(_theme);
        _eventInk = BrushOf(_theme.EventColorHex, "#C62828");
        _dayNumberBrush = BrushOf(_theme.DayColorHex, "#222222");
        _nameDayBrush = BrushOf(_theme.NameDayColorHex, "#5A554C");
        _weekendBg = BrushOf(_theme.WeekendFillHex, "#66F3EEE6");
        _headerBg = BrushOf(_theme.HeaderFillHex, "#AAEFE8DE");
        _cellBorder = BrushOf(_theme.GridLineHex, "#88C9BEAE");

        CalendarPanel.Background = _style.ShowPanelFill
            ? BrushOf(_theme.CalendarPanelHex, "#E6FFFAFA")
            : Brushes.Transparent;
        CalendarPanel.BorderThickness = new Thickness(_style.PanelBorderThickness);
        CalendarPanel.BorderBrush = _style.PanelBorderThickness > 0 ? _cellBorder : Brushes.Transparent;
        CalendarPanel.CornerRadius = new CornerRadius(_style.PanelCornerRadius);
        CalendarPanel.Padding = new Thickness(_style.PanelPadding);

        if (_theme.CalendarFormat == CalendarFormat.Folio)
        {
            CalendarPanel.BorderThickness = new Thickness(2.5);
            CalendarPanel.Padding = new Thickness(5);
        }
    }

    private void ApplyTitleChrome()
    {
        var font = new FontFamily(_theme.FontFamily + ", Georgia, Segoe UI");
        MonthTitle.FontFamily = font;
        YearTitle.FontFamily = font;
        MonthTitle.FontSize = Math.Clamp(_theme.TitleFontSize, 18, 56);
        YearTitle.FontSize = Math.Clamp(_theme.YearFontSize, 10, 28);
        MonthTitle.Foreground = BrushOf(_theme.TitleColorHex, "#2C2C2C");
        YearTitle.Foreground = BrushOf(_theme.YearColorHex, "#555555");
        MonthTitle.FontWeight = _style.BoldDays ? FontWeights.Bold : FontWeights.SemiBold;

        var align = _style.TitleAlignment switch
        {
            TitleAlignmentMode.Left => HorizontalAlignment.Left,
            TitleAlignmentMode.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
        TitleHost.HorizontalAlignment = align;
        MonthTitle.HorizontalAlignment = align;
        YearTitle.HorizontalAlignment = align;

        var magazine = _theme.CalendarFormat == CalendarFormat.Magazine;
        var poster = _theme.CalendarFormat == CalendarFormat.Poster;
        TitleCard.Background = magazine || poster
            ? Brushes.Transparent
            : new SolidColorBrush(Color.FromArgb(204, 255, 255, 255));
        TitleCard.Padding = magazine ? new Thickness(4, 2, 4, 2) : new Thickness(18, 6, 18, 6);
        if (magazine)
            MonthTitle.FontSize = Math.Clamp(_theme.TitleFontSize * 1.15, 22, 60);
    }

    private static SolidColorBrush BrushOf(string? hex, string fallback)
    {
        try
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                string.IsNullOrWhiteSpace(hex) ? fallback : hex)!);
            brush.Freeze();
            return brush;
        }
        catch
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallback)!);
            brush.Freeze();
            return brush;
        }
    }

    private void OnInteractiveLayoutChanged(object? sender, EventArgs e)
        => PhotoLayoutChanged?.Invoke(this, EventArgs.Empty);

    public void RefreshInteractivePhotos()
    {
        if (InteractivePhotos.Visibility == Visibility.Visible)
            InteractivePhotos.RefreshImageContent();
    }

    public void SelectInteractivePhoto(Guid? photoId)
    {
        if (InteractivePhotos.Visibility == Visibility.Visible)
            InteractivePhotos.SelectedPhotoId = photoId;
    }

    public void FinalizeEventLayout()
    {
        EventOverlay.Children.Clear();
        if (_eventCells.Count == 0)
            return;

        // Off-screen PDF/preview renders often leave Canvas.Actual* at 0 until forced.
        var width = EventOverlay.ActualWidth;
        var height = EventOverlay.ActualHeight;
        if (width < 10 || height < 10)
        {
            width = CalendarPanel.ActualWidth;
            height = CalendarPanel.ActualHeight;
        }

        if (EventOverlay.Parent is FrameworkElement host && (width < 10 || height < 10))
        {
            host.UpdateLayout();
            width = host.ActualWidth;
            height = host.ActualHeight;
        }

        if (width < 10 || height < 10)
            return;

        EventOverlay.Width = width;
        EventOverlay.Height = height;

        var headerHeight = Math.Max(28, height * 0.08);
        var bodyHeight = Math.Max(1, height - headerHeight);
        var cellWidth = width / 7.0;
        var cellHeight = bodyHeight / Math.Max(1, _weekRows);
        // Keep event ink below day numbers in this cell and any cell it spills into.
        var dayNumberBand = Math.Clamp(cellHeight * 0.34, 28, 42);
        const double margin = 6;
        var font = new FontFamily(_theme.FontFamily + ", Georgia, Segoe UI");

        var stampedDays = new HashSet<(int Row, int Col)>();

        foreach (var (cell, row, col) in _eventCells)
        {
            var events = cell.Events.Take(3).ToList();
            var seed = cell.Date!.Value.DayNumber;
            var cellLeft = col * cellWidth;
            var cellTop = headerHeight + (row - 1) * cellHeight;

            for (var index = 0; index < events.Count; index++)
            {
                var fontSize = events.Count == 1 ? 20 : events.Count == 2 ? 16 : 14;
                var tilt = ((seed + index * 17) % 7) - 3;
                // Prefer staying near the home cell; slight spill OK if under the number band.
                var desiredMaxWidth = Math.Min(cellWidth * 1.65, width - cellLeft - margin);

                var annotation = CreateEventLabel(
                    events[index], fontSize, desiredMaxWidth, _eventInk);

                annotation.Measure(new Size(desiredMaxWidth, double.PositiveInfinity));
                var textW = Math.Min(desiredMaxWidth, annotation.DesiredSize.Width);
                var textH = annotation.DesiredSize.Height;

                // Start under the day number, left-aligned in the cell (not over the digit).
                var x = cellLeft + 5;
                var y = cellTop + dayNumberBand + index * (fontSize + 6);

                if (x + textW > width - margin)
                {
                    var fit = Math.Max(48, width - margin - x);
                    annotation.MaxWidth = fit;
                    annotation.Measure(new Size(fit, double.PositiveInfinity));
                    textW = Math.Min(fit, annotation.DesiredSize.Width);
                    textH = annotation.DesiredSize.Height;
                }

                // If still too tall for the cell, shrink font rather than climbing onto numbers.
                var maxBottom = cellTop + cellHeight - 4;
                if (y + textH > maxBottom && cellHeight > dayNumberBand + 20)
                {
                    fontSize = Math.Max(11, fontSize - 3);
                    annotation.FontSize = fontSize;
                    annotation.LineHeight = fontSize + 3;
                    annotation.Measure(new Size(annotation.MaxWidth, double.PositiveInfinity));
                    textH = annotation.DesiredSize.Height;
                }

                if (y + textH > height - margin)
                    y = Math.Max(cellTop + dayNumberBand, height - margin - textH);

                var transform = new RotateTransform(tilt);
                var origin = new Point(0, 0.3);
                annotation.RenderTransformOrigin = origin;
                annotation.RenderTransform = transform;

                foreach (var (dx, dy) in new (double, double)[]
                         { (-1.2, 0), (1.2, 0), (0, -1.2), (0, 1.2), (-1, -1), (1, 1) })
                {
                    var ghost = CreateEventLabel(
                        events[index], fontSize, annotation.MaxWidth,
                        new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)));
                    ghost.TextAlignment = annotation.TextAlignment;
                    ghost.RenderTransformOrigin = origin;
                    ghost.RenderTransform = transform;
                    Canvas.SetLeft(ghost, x + dx);
                    Canvas.SetTop(ghost, y + dy);
                    Panel.SetZIndex(ghost, 40 + index);
                    EventOverlay.Children.Add(ghost);
                }

                Canvas.SetLeft(annotation, x);
                Canvas.SetTop(annotation, y);
                Panel.SetZIndex(annotation, 50 + index);
                EventOverlay.Children.Add(annotation);

            }

            // Only the event's own day gets an overlay stamp (circle + digit).
            // Neighbor cells already have their in-cell number — restamping them doubles it.
            if (cell.DayNumber is int homeDay && stampedDays.Add((row, col)))
            {
                StampDayNumberOnTop(
                    homeDay,
                    cellLeft,
                    cellTop,
                    cellWidth,
                    font,
                    z: 90);
            }
        }
    }

    private int? FindDayNumber(int gridRow, int gridCol)
    {
        foreach (var (cell, row, col) in _eventCells)
        {
            if (row == gridRow && col == gridCol && cell.DayNumber is int d)
                return d;
        }

        // Also need numbers for non-event cells that text spills across —
        // resolve from calendar grid children if present.
        foreach (UIElement child in CalendarGrid.Children)
        {
            if (Grid.GetRow(child) != gridRow || Grid.GetColumn(child) != gridCol)
                continue;
            if (child is not Border { Child: Grid inner }) continue;
            foreach (var item in inner.Children)
            {
                if (item is Grid dayPanel)
                {
                    foreach (var part in dayPanel.Children)
                    {
                        if (part is TextBlock tb && int.TryParse(tb.Text, out var n))
                            return n;
                        if (part is Border { Child: TextBlock badgeTb } &&
                            int.TryParse(badgeTb.Text, out var bn))
                            return bn;
                    }
                }
            }
        }

        return null;
    }

    private void StampDayNumberOnTop(
        int dayNumber,
        double cellLeft,
        double cellTop,
        double cellWidth,
        FontFamily font,
        int z)
    {
        var daySize = Math.Clamp(_theme.DayFontSize * (_style.DaySizeFactor), 10, 36);
        var alignCenter = _style.DayAlign == DayAlignMode.Center;
        var weight = _style.BoldDays ? FontWeights.Bold : FontWeights.SemiBold;

        TextBlock MakeDayText(Brush foreground) => new()
        {
            Text = dayNumber.ToString(),
            FontFamily = font,
            FontSize = daySize,
            FontWeight = weight,
            Foreground = foreground,
            IsHitTestVisible = false
        };

        var measure = MakeDayText(_dayNumberBrush);
        measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tw = measure.DesiredSize.Width;
        var th = measure.DesiredSize.Height;

        var diameter = Math.Max(Math.Max(tw, th) + 18, 30);
        var host = new Canvas
        {
            Width = diameter,
            Height = diameter,
            ClipToBounds = false,
            IsHitTestVisible = false
        };

        host.Children.Add(new System.Windows.Shapes.Path
        {
            Stroke = _eventInk,
            StrokeThickness = 2.4,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Data = HandDrawnGeometry.CreateCircle(
                new Rect(1.5, 1.5, diameter - 3, diameter - 3),
                dayNumber),
            IsHitTestVisible = false
        });

        var textLeft = (diameter - tw) / 2;
        var textTop = (diameter - th) / 2 - daySize * 0.06;

        foreach (var (dx, dy) in new (double, double)[]
                 { (-1.1, 0), (1.1, 0), (0, -1.1), (0, 1.1) })
        {
            var halo = MakeDayText(new SolidColorBrush(Color.FromArgb(200, 255, 252, 248)));
            Canvas.SetLeft(halo, textLeft + dx);
            Canvas.SetTop(halo, textTop + dy);
            host.Children.Add(halo);
        }

        var dayText = MakeDayText(_dayNumberBrush);
        Canvas.SetLeft(dayText, textLeft);
        Canvas.SetTop(dayText, textTop);
        host.Children.Add(dayText);

        var x = alignCenter
            ? cellLeft + (cellWidth - diameter) / 2
            : cellLeft + 4;
        var y = cellTop + 3;

        Canvas.SetLeft(host, x);
        Canvas.SetTop(host, y);
        Panel.SetZIndex(host, z);
        EventOverlay.Children.Add(host);
    }

    private static TextBlock CreateEventLabel(string text, double fontSize, double maxWidth, Brush foreground) =>
        new()
        {
            Text = text,
            FontFamily = HandFont,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = fontSize + 4,
            MaxWidth = maxWidth,
            IsHitTestVisible = false
        };

    private void BuildPhotos(MonthPhotos monthPhotos, CollageLayout collage, double width, double height)
    {
        PhotoCanvas.Children.Clear();
        PhotoCanvas.Width = width;
        PhotoCanvas.Height = height;

        if (monthPhotos.Photos.Count == 0)
        {
            EmptyPhotoHint.Visibility = Visibility.Visible;
            return;
        }

        EmptyPhotoHint.Visibility = Visibility.Collapsed;
        var lookup = monthPhotos.Photos.ToDictionary(p => p.Id);

        foreach (var slot in collage.Slots)
        {
            if (!lookup.TryGetValue(slot.PhotoId, out var photo))
                continue;

            var rect = new Rect(
                slot.Bounds.X * width,
                slot.Bounds.Y * height,
                Math.Max(1, slot.Bounds.Width * width),
                Math.Max(1, slot.Bounds.Height * height));

            var borderBrush = BrushOf(_theme.PhotoBorderColorHex, "#FFFFFF");
            var borderThickness = Math.Max(0, _theme.PhotoBorderThickness);
            var cornerRadius = Math.Max(0, _theme.PhotoCornerRadius);
            var frame = new Border
            {
                Width = rect.Width,
                Height = rect.Height,
                CornerRadius = new CornerRadius(cornerRadius),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(borderThickness),
                Background = borderBrush,
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.20,
                    Color = Colors.Black
                }
            };

            var image = new Image
            {
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true
            };

            var loaded = PhotoImageLoader.Load(
                photo,
                decodePixelWidth: Math.Max(128, (int)(Math.Max(rect.Width, rect.Height) * 2.5)));
            if (loaded is null)
                continue;
            image.Source = loaded;

            var scale = Math.Clamp(photo.Zoom, 0.9, 1.25);
            if (Math.Abs(scale - 1.0) > 0.01 ||
                Math.Abs(photo.OffsetX - 0.5) > 0.01 ||
                Math.Abs(photo.OffsetY - 0.5) > 0.01)
            {
                image.RenderTransformOrigin = new Point(0.5, 0.5);
                image.RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new ScaleTransform(scale, scale),
                        new TranslateTransform(
                            (photo.OffsetX - 0.5) * rect.Width * 0.12,
                            (photo.OffsetY - 0.5) * rect.Height * 0.12)
                    }
                };
            }

            frame.Child = image;
            Canvas.SetLeft(frame, rect.X);
            Canvas.SetTop(frame, rect.Y);
            PhotoCanvas.Children.Add(frame);
        }
    }

    private void BuildCalendarGrid(MonthPageModel month)
    {
        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        EventOverlay.Children.Clear();
        _eventCells.Clear();
        _weekRows = month.WeekRows;

        var gap = _style.CellGap;
        CalendarGrid.Margin = new Thickness(gap);

        for (var c = 0; c < 7; c++)
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var r = 0; r < month.WeekRows; r++)
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var font = new FontFamily(_theme.FontFamily + ", Georgia, Segoe UI");
        var weekdaySize = Math.Clamp(_theme.WeekdayFontSize, 8, 20);
        var weekdayColor = BrushOf(_theme.WeekdayColorHex, "#222222");

        for (var c = 0; c < 7; c++)
        {
            var header = CreateWeekdayHeader(month.WeekdayHeaders[c], c < 6, font, weekdaySize, weekdayColor);
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, c);
            CalendarGrid.Children.Add(header);
        }

        for (var i = 0; i < month.Cells.Count; i++)
        {
            var cell = month.Cells[i];
            var row = i / 7 + 1;
            var col = i % 7;
            var border = CreateDayCell(cell, col < 6, font);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            Panel.SetZIndex(border, 0);
            CalendarGrid.Children.Add(border);

            if (cell.HasEvents)
                _eventCells.Add((cell, row, col));
        }
    }

    private Border CreateWeekdayHeader(string text, bool rightBorder, FontFamily font, double size, Brush color)
    {
        var style = _style.HeaderStyle;
        var bg = style switch
        {
            HeaderStyleMode.None => Brushes.Transparent,
            HeaderStyleMode.SolidBar => _headerBg,
            HeaderStyleMode.Pills => _headerBg,
            _ when _style.TransparentHeader => Brushes.Transparent,
            _ => _headerBg
        };

        var borderThickness = style switch
        {
            HeaderStyleMode.None => new Thickness(0),
            HeaderStyleMode.Underline => new Thickness(0, 0, 0, 1.5),
            HeaderStyleMode.Pills => new Thickness(0),
            HeaderStyleMode.SolidBar => new Thickness(0),
            _ when !_style.ShowInnerGrid => new Thickness(0, 0, 0, 1),
            _ => new Thickness(0, 0, rightBorder ? 1 : 0, 1)
        };

        return new Border
        {
            Background = bg,
            BorderBrush = style is HeaderStyleMode.None or HeaderStyleMode.Pills ? Brushes.Transparent : _cellBorder,
            BorderThickness = borderThickness,
            Padding = new Thickness(4, _style.StrongHeader ? 8 : 6, 4, 6),
            Margin = style == HeaderStyleMode.Pills ? new Thickness(1.5) : new Thickness(Math.Max(0, _style.CellGap * 0.3)),
            CornerRadius = style == HeaderStyleMode.Pills
                ? new CornerRadius(10)
                : new CornerRadius(0),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = font,
                FontSize = size,
                FontWeight = _style.StrongHeader ? FontWeights.Bold : FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = color
            }
        };
    }

    private Border CreateDayCell(DayCellModel cell, bool rightBorder, FontFamily font)
    {
        var gap = Math.Max(0, _style.CellGap * 0.5);
        var showGrid = _style.ShowInnerGrid;
        var radius = _style.CellCornerRadius;

        Brush background;
        if (cell.IsEmpty)
            background = Brushes.Transparent;
        else if (cell.IsWeekend && _style.ShowWeekendHighlight)
            background = _weekendBg;
        else if (radius > 0)
            background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        else
            background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));

        var border = new Border
        {
            BorderBrush = showGrid ? _cellBorder : Brushes.Transparent,
            BorderThickness = showGrid
                ? new Thickness(0, 0, rightBorder ? 1 : 0, 1)
                : new Thickness(0),
            Background = background,
            Padding = new Thickness(radius > 0 ? 4 : 3),
            Margin = new Thickness(gap),
            CornerRadius = new CornerRadius(radius),
            ClipToBounds = false
        };

        if (_style.DotSeparators && !cell.IsEmpty)
        {
            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
        }

        if (cell.IsEmpty)
            return border;

        var grid = new Grid { ClipToBounds = false };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (_style.RuledLines)
        {
            for (var i = 1; i <= 3; i++)
            {
                grid.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 2,
                    X2 = 2000,
                    Y1 = 22 + i * 12,
                    Y2 = 22 + i * 12,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 120, 110, 95)),
                    StrokeThickness = 0.8,
                    IsHitTestVisible = false
                });
            }
        }

        if (_style.DotSeparators)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = new SolidColorBrush(Color.FromArgb(90, 100, 90, 80)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 4, 4),
                IsHitTestVisible = false
            };
            Grid.SetRowSpan(dot, 3);
            grid.Children.Add(dot);
        }

        var dayAlign = _style.DayAlign == DayAlignMode.Center
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;

        var dayPanel = new Grid
        {
            HorizontalAlignment = dayAlign,
            Margin = new Thickness(1, 0, 0, 0),
            ClipToBounds = false
        };

        var daySize = Math.Clamp(_theme.DayFontSize * _style.DaySizeFactor, 10, 42);
        var dayText = new TextBlock
        {
            Text = cell.DayNumber!.Value.ToString(),
            FontFamily = font,
            FontSize = daySize,
            FontWeight = _style.BoldDays ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = _dayNumberBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (cell.HasEvents)
        {
            // Invisible spacer — circle + digit are stamped together on the event overlay
            // so the number stays perfectly centered in the ring (above event ink).
            dayText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = dayText.DesiredSize;
            var diameter = Math.Max(Math.Max(size.Width, size.Height) + 18, 30);
            dayPanel.Children.Add(new Border
            {
                Width = diameter,
                Height = diameter,
                Margin = new Thickness(2, 1, 0, 0),
                Opacity = 0,
                IsHitTestVisible = false
            });
        }
        else if (_style.CircularDayBadge)
        {
            dayText.Margin = new Thickness(0);
            var badgeSize = Math.Max(28, daySize + 14);
            dayPanel.Children.Add(new Border
            {
                Width = badgeSize,
                Height = badgeSize,
                CornerRadius = new CornerRadius(badgeSize / 2),
                Background = cell.IsWeekend && _style.ShowWeekendHighlight
                    ? _weekendBg
                    : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderBrush = _cellBorder,
                BorderThickness = new Thickness(1),
                Child = dayText,
                Margin = new Thickness(2)
            });
        }
        else
        {
            dayText.Margin = new Thickness(6, 3, 6, 3);
            dayPanel.Children.Add(dayText);
        }

        Grid.SetRow(dayPanel, 0);
        grid.Children.Add(dayPanel);

        if (_style.ShowNameDays && cell.NameDays.Count > 0)
        {
            var nameText = string.Join(", ", cell.NameDays.Take(3));
            var nameSize = Math.Clamp(_theme.NameDayFontSize, 6, 16);
            if (_theme.CalendarFormat == CalendarFormat.Poster)
                nameSize = Math.Min(nameSize, 7.5);

            var nameBlock = new TextBlock
            {
                Text = nameText,
                FontFamily = font,
                FontSize = nameSize,
                Foreground = _nameDayBrush,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = _style.DayAlign == DayAlignMode.Center ? TextAlignment.Center : TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 2, 2)
            };
            Grid.SetRow(nameBlock, 2);
            grid.Children.Add(nameBlock);
        }

        border.Child = grid;
        return border;
    }
}

