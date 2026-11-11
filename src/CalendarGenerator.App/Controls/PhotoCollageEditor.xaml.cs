using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CalendarGenerator.App.Services;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Photos;

namespace CalendarGenerator.App.Controls;

public partial class PhotoCollageEditor : UserControl
{
    public static readonly DependencyProperty SelectedPhotoIdProperty =
        DependencyProperty.Register(
            nameof(SelectedPhotoId),
            typeof(Guid?),
            typeof(PhotoCollageEditor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedChanged));

    private readonly CollageEngine _collage = new();
    private readonly Dictionary<Guid, PhotoVisual> _visuals = new();
    private IList<PhotoItem> _photos = new List<PhotoItem>();

    private double _frameBorderThickness = 2;
    private string _frameBorderColorHex = "#FFFFFF";
    private double _frameCornerRadius = 2;

    private PhotoVisual? _active;
    private DragMode _mode;
    private Point _startMouse;
    private CollageRect _startBounds;

    public event EventHandler? LayoutChanged;
    public event EventHandler<Guid>? PhotoSelected;

    public PhotoCollageEditor()
    {
        InitializeComponent();
        Surface.MouseMove += Surface_MouseMove;
        Surface.MouseLeftButtonUp += Surface_MouseLeftButtonUp;
        Surface.MouseLeave += (_, _) => { /* keep capture while dragging */ };
    }

    public Guid? SelectedPhotoId
    {
        get => (Guid?)GetValue(SelectedPhotoIdProperty);
        set => SetValue(SelectedPhotoIdProperty, value);
    }

    public void SetFrameStyle(double borderThickness, string? borderColorHex, double cornerRadius)
    {
        _frameBorderThickness = Math.Max(0, borderThickness);
        _frameBorderColorHex = string.IsNullOrWhiteSpace(borderColorHex) ? "#FFFFFF" : borderColorHex;
        _frameCornerRadius = Math.Max(0, cornerRadius);

        foreach (var visual in _visuals.Values)
            ApplyFrameChrome(visual.Frame);
    }

    public void SetPhotos(IList<PhotoItem> photos, Guid? selectedId = null)
    {
        // Don't rebuild while dragging.
        if (_active is not null) return;

        _photos = photos ?? new List<PhotoItem>();
        SelectedPhotoId = selectedId;
        Rebuild();
    }

    public void NudgeSelected(double dx, double dy)
    {
        if (SelectedPhotoId is null) return;
        if (!_visuals.TryGetValue(SelectedPhotoId.Value, out var visual)) return;

        EnsureAllManual();
        var b = visual.Bounds;
        var next = Move(b, dx, dy);
        ApplyBounds(visual, next, preserveAspect: true);
        _collage.ApplyManualLayout(visual.Photo, visual.Bounds);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void OnSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhotoCollageEditor editor)
            editor.UpdateSelectionChrome();
    }

    private double ContainerAspect =>
        Surface.ActualWidth > 1 && Surface.ActualHeight > 1
            ? Surface.ActualWidth / Surface.ActualHeight
            : 1.6;

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_active is not null) return;

        var aspectChanged = e.PreviousSize.Width < 1
            || e.PreviousSize.Height < 1
            || Math.Abs(
                e.NewSize.Width / Math.Max(1, e.NewSize.Height)
                - e.PreviousSize.Width / Math.Max(1, e.PreviousSize.Height)) > 0.02;

        if (aspectChanged)
            RefreshAutoSlots();

        LayoutVisuals();
    }

    private void RefreshAutoSlots()
    {
        if (_photos.Count == 0 || _visuals.Count == 0) return;
        var layout = _collage.Build(_photos.ToList(), ContainerAspect);
        foreach (var slot in layout.Slots)
        {
            if (!_visuals.TryGetValue(slot.PhotoId, out var visual)) continue;
            if (visual.Photo.HasManualLayout) continue;
            visual.Bounds = slot.Bounds;
        }
    }

    private void Rebuild()
    {
        Surface.Children.Clear();
        _visuals.Clear();

        if (_photos.Count == 0)
        {
            Hint.Visibility = Visibility.Visible;
            GapLayer.Children.Clear();
            return;
        }

        Hint.Visibility = Visibility.Collapsed;
        var photoList = _photos.ToList();
        var containerAspect = ContainerAspect;
        _collage.BakeAutomaticLayouts(photoList, containerAspect);
        var layout = _collage.Build(photoList, containerAspect);

        foreach (var slot in layout.Slots)
        {
            var photo = _photos.FirstOrDefault(p => p.Id == slot.PhotoId);
            if (photo is null) continue;

            // Prefer saved manual bounds when present.
            var bounds = photo.HasManualLayout
                ? new CollageRect(photo.LayoutX, photo.LayoutY, photo.LayoutWidth, photo.LayoutHeight)
                : slot.Bounds;

            var visual = CreateVisual(photo, bounds);
            _visuals[photo.Id] = visual;
            Surface.Children.Add(visual.Root);
        }

        LayoutVisuals();
        UpdateSelectionChrome();
    }

    private PhotoVisual CreateVisual(PhotoItem photo, CollageRect bounds)
    {
        var root = new Canvas();

        var frame = new Border
        {
            ClipToBounds = true,
            Cursor = Cursors.SizeAll,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Opacity = 0.25,
                Color = Colors.Black
            }
        };
        ApplyFrameChrome(frame);

        var image = new Image { Stretch = Stretch.Uniform, IsHitTestVisible = false };
        var loaded = PhotoImageLoader.Load(photo, decodePixelWidth: 640);
        if (loaded is not null)
            image.Source = loaded;
        frame.Child = image;

        var selection = new Border
        {
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#8B5E3C")!,
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };

        // Center move affordance (visible when selected)
        var moveBadge = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = (Brush)new BrushConverter().ConvertFromString("#E68B5E3C")!,
            Cursor = Cursors.SizeAll,
            Child = new TextBlock
            {
                Text = "✥",
                Foreground = Brushes.White,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            }
        };

        root.Children.Add(frame);
        root.Children.Add(selection);
        root.Children.Add(moveBadge);

        var handles = new Dictionary<DragMode, Ellipse>();
        foreach (var mode in new[] { DragMode.ResizeNW, DragMode.ResizeNE, DragMode.ResizeSW, DragMode.ResizeSE })
        {
            var handle = CreateHandle(mode);
            handles[mode] = handle;
            root.Children.Add(handle);
        }

        var visual = new PhotoVisual(photo, root, frame, selection, moveBadge, handles, bounds);

        frame.MouseLeftButtonDown += (_, e) => BeginDrag(visual, DragMode.Move, e);
        moveBadge.MouseLeftButtonDown += (_, e) => BeginDrag(visual, DragMode.Move, e);
        foreach (var (mode, handle) in handles)
            handle.MouseLeftButtonDown += (_, e) => BeginDrag(visual, mode, e);

        return visual;
    }

    private static Ellipse CreateHandle(DragMode mode)
    {
        var cursor = mode switch
        {
            DragMode.ResizeNW or DragMode.ResizeSE => Cursors.SizeNWSE,
            _ => Cursors.SizeNESW
        };

        return new Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = Brushes.White,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#8B5E3C")!,
            StrokeThickness = 2,
            Cursor = cursor,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                ShadowDepth = 1,
                Opacity = 0.35
            }
        };
    }

    /// <summary>Reloads image content (e.g. after crop) without resetting layouts.</summary>
    public void RefreshImageContent()
    {
        foreach (var visual in _visuals.Values)
        {
            if (visual.Frame.Child is not Image image) continue;
            var loaded = PhotoImageLoader.Load(visual.Photo, decodePixelWidth: 640);
            if (loaded is not null)
                image.Source = loaded;
        }
    }

    private void LayoutVisuals()
    {
        if (Surface.ActualWidth <= 1 || Surface.ActualHeight <= 1)
            return;

        foreach (var visual in _visuals.Values)
            ApplyBounds(visual, visual.Bounds, preserveAspect: true);

        UpdateGapGuides();
    }

    private void UpdateGapGuides()
    {
        GapLayer.Children.Clear();
        GapLayer.Width = Surface.ActualWidth;
        GapLayer.Height = Surface.ActualHeight;

        if (_visuals.Count < 2 || Surface.ActualWidth < 8 || Surface.ActualHeight < 8)
            return;

        var rects = _visuals.Values
            .Select(v => new Rect(
                v.Bounds.X * Surface.ActualWidth,
                v.Bounds.Y * Surface.ActualHeight,
                Math.Max(24, v.Bounds.Width * Surface.ActualWidth),
                Math.Max(24, v.Bounds.Height * Surface.ActualHeight)))
            .ToList();

        var guideBrush = (Brush)new BrushConverter().ConvertFromString("#C47A3A")!;
        const double maxGapFraction = 0.45;

        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];

                // Horizontal gap (side-by-side, vertically overlapping)
                var yOverlap = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
                if (yOverlap > 8)
                {
                    var left = a.Right <= b.Left ? a : b;
                    var right = a.Right <= b.Left ? b : a;
                    var gap = right.Left - left.Right;
                    if (gap > 0.5 && gap < Surface.ActualWidth * maxGapFraction)
                    {
                        var midY = (Math.Max(a.Top, b.Top) + Math.Min(a.Bottom, b.Bottom)) / 2;
                        DrawHorizontalGap(left.Right, right.Left, midY, gap, guideBrush);
                    }
                }

                // Vertical gap (stacked, horizontally overlapping)
                var xOverlap = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
                if (xOverlap > 8)
                {
                    var top = a.Bottom <= b.Top ? a : b;
                    var bottom = a.Bottom <= b.Top ? b : a;
                    var gap = bottom.Top - top.Bottom;
                    if (gap > 0.5 && gap < Surface.ActualHeight * maxGapFraction)
                    {
                        var midX = (Math.Max(a.Left, b.Left) + Math.Min(a.Right, b.Right)) / 2;
                        DrawVerticalGap(top.Bottom, bottom.Top, midX, gap, guideBrush);
                    }
                }
            }
        }
    }

    private void DrawHorizontalGap(double x1, double x2, double y, double gapPx, Brush brush)
    {
        const double tick = 5;
        GapLayer.Children.Add(new Line
        {
            X1 = x1, Y1 = y, X2 = x2, Y2 = y,
            Stroke = brush, StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        });
        GapLayer.Children.Add(new Line
        {
            X1 = x1, Y1 = y - tick, X2 = x1, Y2 = y + tick,
            Stroke = brush, StrokeThickness = 1.4
        });
        GapLayer.Children.Add(new Line
        {
            X1 = x2, Y1 = y - tick, X2 = x2, Y2 = y + tick,
            Stroke = brush, StrokeThickness = 1.4
        });

        var label = CreateGapLabel(FormatGapMm(gapPx));
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, (x1 + x2) / 2 - label.DesiredSize.Width / 2);
        Canvas.SetTop(label, y - label.DesiredSize.Height - 2);
        GapLayer.Children.Add(label);
    }

    private void DrawVerticalGap(double y1, double y2, double x, double gapPx, Brush brush)
    {
        const double tick = 5;
        GapLayer.Children.Add(new Line
        {
            X1 = x, Y1 = y1, X2 = x, Y2 = y2,
            Stroke = brush, StrokeThickness = 1.2,
            StrokeDashArray = new DoubleCollection { 3, 2 }
        });
        GapLayer.Children.Add(new Line
        {
            X1 = x - tick, Y1 = y1, X2 = x + tick, Y2 = y1,
            Stroke = brush, StrokeThickness = 1.4
        });
        GapLayer.Children.Add(new Line
        {
            X1 = x - tick, Y1 = y2, X2 = x + tick, Y2 = y2,
            Stroke = brush, StrokeThickness = 1.4
        });

        var label = CreateGapLabel(FormatGapMm(gapPx));
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, x + 6);
        Canvas.SetTop(label, (y1 + y2) / 2 - label.DesiredSize.Height / 2);
        GapLayer.Children.Add(label);
    }

    private static Border CreateGapLabel(string text) =>
        new()
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 255, 252, 248)),
            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#C47A3A")!,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)new BrushConverter().ConvertFromString("#8B5E3C")!,
                FontFamily = new FontFamily("Segoe UI")
            }
        };

    private static string FormatGapMm(double gapDip)
    {
        // WPF DIP: 96 units ≈ 1 inch
        var mm = gapDip / 96.0 * 25.4;
        return mm < 10 ? $"{mm:0.0} mm" : $"{mm:0} mm";
    }

    private void ApplyBounds(PhotoVisual visual, CollageRect bounds, bool preserveAspect)
    {
        visual.Bounds = preserveAspect
            ? CollageEngine.KeepPixelAspect(bounds, visual.Photo.AspectRatio, ContainerAspect)
            : Clamp(bounds);

        var x = visual.Bounds.X * Surface.ActualWidth;
        var y = visual.Bounds.Y * Surface.ActualHeight;
        var w = Math.Max(24, visual.Bounds.Width * Surface.ActualWidth);
        var h = Math.Max(24, visual.Bounds.Height * Surface.ActualHeight);

        Canvas.SetLeft(visual.Root, x);
        Canvas.SetTop(visual.Root, y);
        visual.Root.Width = w;
        visual.Root.Height = h;

        visual.Frame.Width = w;
        visual.Frame.Height = h;
        visual.Selection.Width = w;
        visual.Selection.Height = h;

        Canvas.SetLeft(visual.MoveBadge, (w - visual.MoveBadge.Width) / 2);
        Canvas.SetTop(visual.MoveBadge, (h - visual.MoveBadge.Height) / 2);

        PlaceHandle(visual.Handles[DragMode.ResizeNW], -6, -6);
        PlaceHandle(visual.Handles[DragMode.ResizeNE], w - 10, -6);
        PlaceHandle(visual.Handles[DragMode.ResizeSW], -6, h - 10);
        PlaceHandle(visual.Handles[DragMode.ResizeSE], w - 10, h - 10);
    }

    private static void PlaceHandle(Ellipse handle, double x, double y)
    {
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, y);
    }

    private void BeginDrag(PhotoVisual visual, DragMode mode, MouseButtonEventArgs e)
    {
        SelectedPhotoId = visual.Photo.Id;
        PhotoSelected?.Invoke(this, visual.Photo.Id);
        UpdateSelectionChrome();

        EnsureAllManual();
        // Re-sync start bounds after baking, and refresh on-screen positions.
        foreach (var v in _visuals.Values)
            ApplyBounds(v, v.Bounds, preserveAspect: true);

        _active = visual;
        _mode = mode;
        _startMouse = e.GetPosition(Surface);
        _startBounds = visual.Bounds;

        Surface.CaptureMouse();
        Panel.SetZIndex(visual.Root, 100);
        e.Handled = true;
    }

    private void EnsureAllManual()
    {
        var photoList = _photos.ToList();
        _collage.BakeAutomaticLayouts(photoList, ContainerAspect);
        foreach (var photo in photoList)
        {
            if (!photo.HasManualLayout)
                photo.HasManualLayout = true;
        }

        foreach (var visual in _visuals.Values)
        {
            var p = visual.Photo;
            visual.Bounds = CollageEngine.KeepPixelAspect(
                new CollageRect(p.LayoutX, p.LayoutY, p.LayoutWidth, p.LayoutHeight),
                p.AspectRatio,
                ContainerAspect);
            _collage.ApplyManualLayout(p, visual.Bounds);
        }
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_active is null || e.LeftButton != MouseButtonState.Pressed)
            return;
        if (Surface.ActualWidth < 1 || Surface.ActualHeight < 1)
            return;

        var pos = e.GetPosition(Surface);
        var dx = (pos.X - _startMouse.X) / Surface.ActualWidth;
        var dy = (pos.Y - _startMouse.Y) / Surface.ActualHeight;

        var next = _mode switch
        {
            DragMode.Move => Move(_startBounds, dx, dy),
            DragMode.ResizeNW => ResizeCorner(_startBounds, dx, dy, fromLeft: true, fromTop: true),
            DragMode.ResizeNE => ResizeCorner(_startBounds, dx, dy, fromLeft: false, fromTop: true),
            DragMode.ResizeSW => ResizeCorner(_startBounds, dx, dy, fromLeft: true, fromTop: false),
            DragMode.ResizeSE => ResizeCorner(_startBounds, dx, dy, fromLeft: false, fromTop: false),
            _ => _startBounds
        };

        // While moving, do not re-derive height from aspect (avoids position jumps).
        ApplyBounds(_active, next, preserveAspect: _mode != DragMode.Move);

        // Live-save position/size onto the model so preview/PDF stay in sync.
        _collage.ApplyManualLayout(_active.Photo, _active.Bounds);
        UpdateGapGuides();
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_active is null) return;

        Surface.ReleaseMouseCapture();
        _collage.ApplyManualLayout(_active.Photo, _active.Bounds);
        UpdateSelectionChrome();
        _active = null;
        UpdateGapGuides();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static CollageRect Move(CollageRect start, double dx, double dy)
    {
        var x = Math.Clamp(start.X + dx, 0, Math.Max(0, 1 - start.Width));
        var y = Math.Clamp(start.Y + dy, 0, Math.Max(0, 1 - start.Height));
        return new CollageRect(x, y, start.Width, start.Height);
    }

    private static CollageRect ResizeCorner(
        CollageRect start,
        double dx,
        double dy,
        bool fromLeft,
        bool fromTop)
    {
        const double min = 0.08;
        var aspect = Math.Max(0.35, start.Width / Math.Max(0.0001, start.Height));

        var widthDelta = fromLeft ? -dx : dx;
        var heightAsWidthDelta = (fromTop ? -dy : dy) * aspect;
        var delta = Math.Abs(widthDelta) >= Math.Abs(heightAsWidthDelta)
            ? widthDelta
            : heightAsWidthDelta;

        var width = Math.Clamp(start.Width + delta, min, 1);
        var height = width / aspect;
        if (height > 1)
        {
            height = 1;
            width = height * aspect;
        }

        var x = fromLeft ? start.X + start.Width - width : start.X;
        var y = fromTop ? start.Y + start.Height - height : start.Y;
        x = Math.Clamp(x, 0, 1 - width);
        y = Math.Clamp(y, 0, 1 - height);
        return new CollageRect(x, y, width, height);
    }

    private static CollageRect Clamp(CollageRect bounds)
    {
        var w = Math.Clamp(bounds.Width, 0.05, 1);
        var h = Math.Clamp(bounds.Height, 0.05, 1);
        var x = Math.Clamp(bounds.X, 0, 1 - w);
        var y = Math.Clamp(bounds.Y, 0, 1 - h);
        return new CollageRect(x, y, w, h);
    }

    private void UpdateSelectionChrome()
    {
        foreach (var visual in _visuals.Values)
        {
            var selected = SelectedPhotoId == visual.Photo.Id;
            visual.Selection.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            visual.MoveBadge.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            Panel.SetZIndex(visual.Root, selected ? 20 : 1);
        }
    }

    private void ApplyFrameChrome(Border frame)
    {
        var brush = BrushFromHex(_frameBorderColorHex, Colors.White);
        frame.BorderBrush = brush;
        frame.Background = brush;
        frame.BorderThickness = new Thickness(_frameBorderThickness);
        frame.CornerRadius = new CornerRadius(_frameCornerRadius);
    }

    private static SolidColorBrush BrushFromHex(string? hex, Color fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
            {
                var parsed = (Color)ColorConverter.ConvertFromString(hex)!;
                var brush = new SolidColorBrush(parsed);
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // fall through
        }

        var fallbackBrush = new SolidColorBrush(fallback);
        fallbackBrush.Freeze();
        return fallbackBrush;
    }

    private enum DragMode
    {
        Move,
        ResizeNW,
        ResizeNE,
        ResizeSW,
        ResizeSE
    }

    private sealed class PhotoVisual
    {
        public PhotoVisual(
            PhotoItem photo,
            Canvas root,
            Border frame,
            Border selection,
            Border moveBadge,
            Dictionary<DragMode, Ellipse> handles,
            CollageRect bounds)
        {
            Photo = photo;
            Root = root;
            Frame = frame;
            Selection = selection;
            MoveBadge = moveBadge;
            Handles = handles;
            Bounds = bounds;
        }

        public PhotoItem Photo { get; }
        public Canvas Root { get; }
        public Border Frame { get; }
        public Border Selection { get; }
        public Border MoveBadge { get; }
        public Dictionary<DragMode, Ellipse> Handles { get; }
        public CollageRect Bounds { get; set; }
    }
}
