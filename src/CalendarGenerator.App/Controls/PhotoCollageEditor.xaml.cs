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
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(2),
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
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_active is null) return;

        Surface.ReleaseMouseCapture();
        _collage.ApplyManualLayout(_active.Photo, _active.Bounds);
        UpdateSelectionChrome();
        _active = null;
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
