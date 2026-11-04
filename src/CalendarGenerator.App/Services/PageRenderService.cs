using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CalendarGenerator.App.Controls;
using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.Photos;
using CalendarGenerator.Core.Templates;

namespace CalendarGenerator.App.Services;

public sealed class PageRenderService
{
    private readonly CollageEngine _collageEngine = new();
    private readonly TemplateLibrary _templates = new();

    public const double Dpi = 300;

    public Size GetPageSizeDip(PaperSize paper)
    {
        paper.ApplyPreset();
        // 1 inch = 25.4 mm; WPF DIP = 1/96 inch
        var widthDip = paper.WidthMm / 25.4 * 96;
        var heightDip = paper.HeightMm / 25.4 * 96;
        return new Size(widthDip, heightDip);
    }

    public static (double Width, double Height) GetPhotoAreaSize(Size pageSize)
    {
        const double assemblyCm = 1.2;
        var assemblyDip = assemblyCm / 2.54 * 96.0;
        var contentHeight = Math.Max(200, pageSize.Height - assemblyDip);
        var photoAreaHeight = Math.Max(120, contentHeight * 0.40);
        return (Math.Max(1, pageSize.Width - 44), photoAreaHeight);
    }

    public CalendarPageControl CreatePageVisual(CalendarProject project, MonthPageModel month)
    {
        var size = GetPageSizeDip(project.Paper);
        var monthPhotos = project.GetMonth(month.Month);
        var theme = monthPhotos.GetOrCreateTheme(_templates, project.CustomTemplates);
        var (photoW, photoH) = GetPhotoAreaSize(size);
        var collage = _collageEngine.Build(monthPhotos.Photos, photoW / photoH);

        var control = new CalendarPageControl();
        control.Width = size.Width;
        control.Height = size.Height;
        control.Measure(size);
        control.Arrange(new Rect(size));
        control.UpdateLayout();
        control.Bind(month, monthPhotos, collage, theme, size);
        control.UpdateLayout();
        control.FinalizeEventLayout();
        return control;
    }

    public BitmapSource RenderMonth(CalendarProject project, MonthPageModel month, double dpi = Dpi)
    {
        var visual = CreatePageVisual(project, month);
        var size = GetPageSizeDip(project.Paper);
        var scale = dpi / 96.0;
        var pixelWidth = (int)Math.Ceiling(size.Width * scale);
        var pixelHeight = (int)Math.Ceiling(size.Height * scale);

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        visual.Measure(size);
        visual.Arrange(new Rect(size));
        visual.UpdateLayout();
        // Second layout pass so EventOverlay gets real size before labels are placed.
        visual.FinalizeEventLayout();
        visual.UpdateLayout();
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    public async Task<string> RenderMonthToPngAsync(CalendarProject project, MonthPageModel month, string outputPath, double dpi = Dpi)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var bitmap = await Application.Current.Dispatcher.InvokeAsync(() => RenderMonth(project, month, dpi));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var stream = File.Create(outputPath);
        encoder.Save(stream);
        return outputPath;
    }
}
