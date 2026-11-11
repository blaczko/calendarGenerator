using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CalendarGenerator.App.Controls;

public partial class HexColorPicker : UserControl
{
    public static readonly DependencyProperty HexColorProperty =
        DependencyProperty.Register(
            nameof(HexColor),
            typeof(string),
            typeof(HexColorPicker),
            new FrameworkPropertyMetadata(
                "#000000",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHexColorChanged));

    private double _hue; // 0–360
    private double _sat = 1; // 0–1
    private double _val = 1; // 0–1
    private bool _svDragging;
    private bool _hueDragging;
    private bool _suppressUi;

    public HexColorPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyFromHex(HexColor);
    }

    public string HexColor
    {
        get => (string)GetValue(HexColorProperty);
        set => SetValue(HexColorProperty, value);
    }

    private static void OnHexColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HexColorPicker picker && !picker._suppressUi)
            picker.ApplyFromHex(e.NewValue as string);
    }

    private void SwatchButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyFromHex(HexColor);
        PickerPopup.IsOpen = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => PickerPopup.IsOpen = false;

    private void ApplyFromHex(string? hex)
    {
        if (!TryParseHex(hex, out var color))
            color = Colors.Black;

        RgbToHsv(color.R, color.G, color.B, out _hue, out _sat, out _val);
        RefreshUi(pushProperty: false);
    }

    private void CommitFromHsv()
    {
        var color = HsvToColor(_hue, _sat, _val);
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _suppressUi = true;
        HexColor = hex;
        _suppressUi = false;
        RefreshUi(pushProperty: false);
    }

    private void RefreshUi(bool pushProperty)
    {
        var color = HsvToColor(_hue, _sat, _val);
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        var brush = new SolidColorBrush(color);

        SwatchPreview.Background = brush;
        ResultSwatch.Background = brush;
        HexLabel.Text = hex;
        if (!HexBox.IsFocused)
            HexBox.Text = hex;
        if (!RBox.IsFocused) RBox.Text = color.R.ToString(CultureInfo.InvariantCulture);
        if (!GBox.IsFocused) GBox.Text = color.G.ToString(CultureInfo.InvariantCulture);
        if (!BBox.IsFocused) BBox.Text = color.B.ToString(CultureInfo.InvariantCulture);

        var hueColor = HsvToColor(_hue, 1, 1);
        HueBase.Background = new SolidColorBrush(hueColor);

        if (SvField.ActualWidth > 1 && SvField.ActualHeight > 1)
        {
            Canvas.SetLeft(SvCursor, _sat * SvField.ActualWidth - SvCursor.Width / 2);
            Canvas.SetTop(SvCursor, (1 - _val) * SvField.ActualHeight - SvCursor.Height / 2);
        }

        if (HueBar.ActualWidth > 1)
            Canvas.SetLeft(HueCursor, (_hue / 360.0) * HueBar.ActualWidth - HueCursor.Width / 2);

        if (pushProperty)
        {
            _suppressUi = true;
            HexColor = hex;
            _suppressUi = false;
        }
    }

    private void SvField_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshUi(pushProperty: false);

    private void SvField_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _svDragging = true;
        SvField.CaptureMouse();
        UpdateSvFromMouse(e.GetPosition(SvField));
    }

    private void SvField_MouseMove(object sender, MouseEventArgs e)
    {
        if (_svDragging && e.LeftButton == MouseButtonState.Pressed)
            UpdateSvFromMouse(e.GetPosition(SvField));
    }

    private void SvField_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_svDragging) return;
        _svDragging = false;
        SvField.ReleaseMouseCapture();
    }

    private void UpdateSvFromMouse(Point p)
    {
        var w = Math.Max(1, SvField.ActualWidth);
        var h = Math.Max(1, SvField.ActualHeight);
        _sat = Math.Clamp(p.X / w, 0, 1);
        _val = Math.Clamp(1 - p.Y / h, 0, 1);
        CommitFromHsv();
    }

    private void HueBar_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshUi(pushProperty: false);

    private void HueBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hueDragging = true;
        HueBar.CaptureMouse();
        UpdateHueFromMouse(e.GetPosition(HueBar));
    }

    private void HueBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_hueDragging && e.LeftButton == MouseButtonState.Pressed)
            UpdateHueFromMouse(e.GetPosition(HueBar));
    }

    private void HueBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_hueDragging) return;
        _hueDragging = false;
        HueBar.ReleaseMouseCapture();
    }

    private void UpdateHueFromMouse(Point p)
    {
        var w = Math.Max(1, HueBar.ActualWidth);
        _hue = Math.Clamp(p.X / w, 0, 1) * 360.0;
        CommitFromHsv();
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyTypedHex();

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyTypedHex();
            e.Handled = true;
        }
    }

    private void ApplyTypedHex()
    {
        var text = HexBox.Text?.Trim() ?? "";
        if (!text.StartsWith('#'))
            text = "#" + text;
        if (TryParseHex(text, out _))
        {
            ApplyFromHex(text);
            CommitFromHsv();
        }
        else
        {
            RefreshUi(pushProperty: false);
        }
    }

    private void RgbBox_LostFocus(object sender, RoutedEventArgs e) => ApplyTypedRgb();

    private void RgbBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyTypedRgb();
            e.Handled = true;
        }
    }

    private void ApplyTypedRgb()
    {
        if (!byte.TryParse(RBox.Text, out var r) ||
            !byte.TryParse(GBox.Text, out var g) ||
            !byte.TryParse(BBox.Text, out var b))
        {
            RefreshUi(pushProperty: false);
            return;
        }

        RgbToHsv(r, g, b, out _hue, out _sat, out _val);
        CommitFromHsv();
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        try
        {
            var value = hex.Trim();
            if (!value.StartsWith('#'))
                value = "#" + value;
            if (value.Length is not (4 or 7 or 9))
                return false;
            color = (Color)ColorConverter.ConvertFromString(value)!;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
    {
        var rd = r / 255.0;
        var gd = g / 255.0;
        var bd = b / 255.0;
        var max = Math.Max(rd, Math.Max(gd, bd));
        var min = Math.Min(rd, Math.Min(gd, bd));
        var delta = max - min;

        v = max;
        s = max <= 0 ? 0 : delta / max;

        if (delta <= 0.00001)
        {
            h = 0;
            return;
        }

        if (Math.Abs(max - rd) < 0.00001)
            h = 60 * (((gd - bd) / delta) % 6);
        else if (Math.Abs(max - gd) < 0.00001)
            h = 60 * (((bd - rd) / delta) + 2);
        else
            h = 60 * (((rd - gd) / delta) + 4);

        if (h < 0) h += 360;
    }

    private static Color HsvToColor(double h, double s, double v)
    {
        h = (h % 360 + 360) % 360;
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round((r + m) * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round((g + m) * 255), 0, 255),
            (byte)Math.Clamp((int)Math.Round((b + m) * 255), 0, 255));
    }
}
