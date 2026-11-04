using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CalendarGenerator.Core.Models;

namespace CalendarGenerator.App.Controls;

public partial class PhotoCropEditor : UserControl
{
    private PhotoItem? _photo;
    private BitmapSource? _source;
    private Rect _imageRect;   // image display rect inside Surface
    private Rect _cropNorm = new(0, 0, 1, 1);

    private Border? _dimLeft, _dimTop, _dimRight, _dimBottom;
    private Border? _cropFrame;
    private readonly Dictionary<string, Ellipse> _handles = new();

    private string? _dragMode;
    private Point _startMouse;
    private Rect _startCrop;

    public event EventHandler? CropChanged;

    public PhotoCropEditor()
    {
        InitializeComponent();
        Surface.MouseMove += Surface_MouseMove;
        Surface.MouseLeftButtonUp += Surface_MouseLeftButtonUp;
    }

    public void SetPhoto(PhotoItem? photo)
    {
        _photo = photo;
        _source = null;
        Surface.Children.Clear();
        _handles.Clear();

        if (photo is null)
        {
            EmptyHint.Visibility = Visibility.Visible;
            EditorRoot.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyHint.Visibility = Visibility.Collapsed;
        EditorRoot.Visibility = Visibility.Visible;

        var path = !string.IsNullOrWhiteSpace(photo.ProjectCopyPath) && File.Exists(photo.ProjectCopyPath)
            ? photo.ProjectCopyPath!
            : photo.SourcePath;

        if (!File.Exists(path))
        {
            EmptyHint.Visibility = Visibility.Visible;
            EditorRoot.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            bi.DecodePixelWidth = 1200;
            bi.EndInit();
            bi.Freeze();
            _source = bi;
        }
        catch
        {
            EmptyHint.Visibility = Visibility.Visible;
            EditorRoot.Visibility = Visibility.Collapsed;
            return;
        }

        _cropNorm = new Rect(
            Math.Clamp(photo.CropX, 0, 0.95),
            Math.Clamp(photo.CropY, 0, 0.95),
            Math.Clamp(photo.CropWidth, 0.05, 1),
            Math.Clamp(photo.CropHeight, 0.05, 1));
        NormalizeCrop(ref _cropNorm);

        BuildChrome();
        LayoutChrome();
    }

    private void BuildChrome()
    {
        Surface.Children.Clear();
        _handles.Clear();
        if (_source is null) return;

        var image = new Image
        {
            Source = _source,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        Surface.Children.Add(image);

        _dimLeft = MakeDim();
        _dimTop = MakeDim();
        _dimRight = MakeDim();
        _dimBottom = MakeDim();
        Surface.Children.Add(_dimLeft);
        Surface.Children.Add(_dimTop);
        Surface.Children.Add(_dimRight);
        Surface.Children.Add(_dimBottom);

        _cropFrame = new Border
        {
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            Cursor = Cursors.SizeAll
        };
        _cropFrame.MouseLeftButtonDown += (_, e) => BeginDrag("move", e);
        Surface.Children.Add(_cropFrame);

        foreach (var corner in new[] { "nw", "ne", "sw", "se" })
        {
            var handle = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.White,
                Stroke = (Brush)new BrushConverter().ConvertFromString("#8B5E3C")!,
                StrokeThickness = 2,
                Cursor = corner is "nw" or "se" ? Cursors.SizeNWSE : Cursors.SizeNESW
            };
            var mode = corner;
            handle.MouseLeftButtonDown += (_, e) => BeginDrag(mode, e);
            _handles[corner] = handle;
            Surface.Children.Add(handle);
        }
    }

    private static Border MakeDim() => new()
    {
        Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
        IsHitTestVisible = false
    };

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutChrome();

    private void LayoutChrome()
    {
        if (_source is null || Surface.ActualWidth < 2 || Surface.ActualHeight < 2)
            return;

        // Fit image uniformly into surface
        var imgAspect = _source.PixelWidth / (double)_source.PixelHeight;
        var surfAspect = Surface.ActualWidth / Surface.ActualHeight;
        double w, h, x, y;
        if (imgAspect > surfAspect)
        {
            w = Surface.ActualWidth;
            h = w / imgAspect;
            x = 0;
            y = (Surface.ActualHeight - h) / 2;
        }
        else
        {
            h = Surface.ActualHeight;
            w = h * imgAspect;
            y = 0;
            x = (Surface.ActualWidth - w) / 2;
        }

        _imageRect = new Rect(x, y, w, h);

        if (Surface.Children[0] is Image image)
        {
            Canvas.SetLeft(image, x);
            Canvas.SetTop(image, y);
            image.Width = w;
            image.Height = h;
        }

        var crop = NormToSurface(_cropNorm);
        PlaceDims(crop);
        if (_cropFrame is not null)
        {
            Canvas.SetLeft(_cropFrame, crop.X);
            Canvas.SetTop(_cropFrame, crop.Y);
            _cropFrame.Width = crop.Width;
            _cropFrame.Height = crop.Height;
        }

        PlaceHandle("nw", crop.X - 6, crop.Y - 6);
        PlaceHandle("ne", crop.Right - 8, crop.Y - 6);
        PlaceHandle("sw", crop.X - 6, crop.Bottom - 8);
        PlaceHandle("se", crop.Right - 8, crop.Bottom - 8);
    }

    private void PlaceDims(Rect crop)
    {
        if (_dimLeft is null || _dimTop is null || _dimRight is null || _dimBottom is null)
            return;

        SetBox(_dimLeft, _imageRect.X, _imageRect.Y, Math.Max(0, crop.X - _imageRect.X), _imageRect.Height);
        SetBox(_dimRight, crop.Right, _imageRect.Y, Math.Max(0, _imageRect.Right - crop.Right), _imageRect.Height);
        SetBox(_dimTop, crop.X, _imageRect.Y, crop.Width, Math.Max(0, crop.Y - _imageRect.Y));
        SetBox(_dimBottom, crop.X, crop.Bottom, crop.Width, Math.Max(0, _imageRect.Bottom - crop.Bottom));
    }

    private static void SetBox(Border box, double x, double y, double w, double h)
    {
        Canvas.SetLeft(box, x);
        Canvas.SetTop(box, y);
        box.Width = Math.Max(0, w);
        box.Height = Math.Max(0, h);
    }

    private void PlaceHandle(string key, double x, double y)
    {
        if (!_handles.TryGetValue(key, out var handle)) return;
        Canvas.SetLeft(handle, x);
        Canvas.SetTop(handle, y);
    }

    private Rect NormToSurface(Rect norm) => new(
        _imageRect.X + norm.X * _imageRect.Width,
        _imageRect.Y + norm.Y * _imageRect.Height,
        norm.Width * _imageRect.Width,
        norm.Height * _imageRect.Height);

    private Rect SurfaceToNorm(Rect surface)
    {
        if (_imageRect.Width < 1 || _imageRect.Height < 1)
            return new Rect(0, 0, 1, 1);

        var r = new Rect(
            (surface.X - _imageRect.X) / _imageRect.Width,
            (surface.Y - _imageRect.Y) / _imageRect.Height,
            surface.Width / _imageRect.Width,
            surface.Height / _imageRect.Height);
        NormalizeCrop(ref r);
        return r;
    }

    private static void NormalizeCrop(ref Rect r)
    {
        const double min = 0.05;
        r.Width = Math.Clamp(r.Width, min, 1);
        r.Height = Math.Clamp(r.Height, min, 1);
        r.X = Math.Clamp(r.X, 0, 1 - r.Width);
        r.Y = Math.Clamp(r.Y, 0, 1 - r.Height);
    }

    private void BeginDrag(string mode, MouseButtonEventArgs e)
    {
        _dragMode = mode;
        _startMouse = e.GetPosition(Surface);
        _startCrop = NormToSurface(_cropNorm);
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var pos = e.GetPosition(Surface);
        var dx = pos.X - _startMouse.X;
        var dy = pos.Y - _startMouse.Y;
        var crop = _startCrop;

        switch (_dragMode)
        {
            case "move":
                crop.X = _startCrop.X + dx;
                crop.Y = _startCrop.Y + dy;
                crop.X = Math.Clamp(crop.X, _imageRect.X, _imageRect.Right - crop.Width);
                crop.Y = Math.Clamp(crop.Y, _imageRect.Y, _imageRect.Bottom - crop.Height);
                break;
            case "nw":
                crop = Resize(crop, dx, dy, fromLeft: true, fromTop: true);
                break;
            case "ne":
                crop = Resize(crop, dx, dy, fromLeft: false, fromTop: true);
                break;
            case "sw":
                crop = Resize(crop, dx, dy, fromLeft: true, fromTop: false);
                break;
            case "se":
                crop = Resize(crop, dx, dy, fromLeft: false, fromTop: false);
                break;
        }

        _cropNorm = SurfaceToNorm(crop);
        LayoutChrome();
        PersistCrop(notify: false);
    }

    private Rect Resize(Rect start, double dx, double dy, bool fromLeft, bool fromTop)
    {
        const double minPx = 24;
        var left = start.X;
        var top = start.Y;
        var right = start.Right;
        var bottom = start.Bottom;

        if (fromLeft) left = Math.Clamp(start.X + dx, _imageRect.X, right - minPx);
        else right = Math.Clamp(start.Right + dx, left + minPx, _imageRect.Right);

        if (fromTop) top = Math.Clamp(start.Y + dy, _imageRect.Y, bottom - minPx);
        else bottom = Math.Clamp(start.Bottom + dy, top + minPx, _imageRect.Bottom);

        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode is null) return;
        _dragMode = null;
        Surface.ReleaseMouseCapture();
        PersistCrop(notify: true);
        e.Handled = true;
    }

    private void ResetCrop_Click(object sender, RoutedEventArgs e)
    {
        _cropNorm = new Rect(0, 0, 1, 1);
        LayoutChrome();
        PersistCrop(notify: true);
    }

    private void PersistCrop(bool notify)
    {
        if (_photo is null) return;
        _photo.CropX = _cropNorm.X;
        _photo.CropY = _cropNorm.Y;
        _photo.CropWidth = _cropNorm.Width;
        _photo.CropHeight = _cropNorm.Height;

        // Update aspect based on cropped region of the original image.
        if (_source is not null && _cropNorm.Height > 0)
        {
            var fullAspect = _source.PixelWidth / (double)_source.PixelHeight;
            _photo.AspectRatio = fullAspect * (_cropNorm.Width / _cropNorm.Height);
        }

        if (notify)
            CropChanged?.Invoke(this, EventArgs.Empty);
    }
}
