using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CalendarGenerator.App.Services;

public sealed class PdfExportService
{
    private readonly PageRenderService _pageRender;
    private readonly CalendarEngine _calendarEngine;

    /// <summary>Preview strip scale relative to 300 DPI page renders (keeps long JPEG manageable).</summary>
    private const double PreviewStripScale = 0.4;

    public PdfExportService(PageRenderService pageRender, CalendarEngine calendarEngine)
    {
        _pageRender = pageRender;
        _calendarEngine = calendarEngine;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task ExportAsync(
        CalendarProject project,
        string outputPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var tempDir = Path.Combine(Path.GetTempPath(), "CalendarGenerator", project.Id.ToString("N"), "clean-only");
        Directory.CreateDirectory(tempDir);
        try
        {
            var pages = await RenderMonthPagesAsync(project, tempDir, progress, ct);
            await WriteCleanPdfAsync(project, pages, outputPath, progress, ct);
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    /// <summary>
    /// Exports a clean PDF and one long preview JPEG (all 12 pages stacked, no watermark).
    /// Paths: {orderId}_{A4|A3|A5}_{portrait|landscape}.pdf and …_preview.jpg
    /// </summary>
    public async Task<(string CleanPath, string PreviewStripPath)> ExportOrderPairAsync(
        CalendarProject project,
        string outputFolder,
        string orderId,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        project.Paper.ApplyPreset();
        var baseName = BuildExportBaseName(orderId, project);
        var cleanPath = Path.Combine(outputFolder, $"{baseName}.pdf");
        var stripPath = Path.Combine(outputFolder, $"{baseName}_preview.jpg");

        var tempDir = Path.Combine(Path.GetTempPath(), "CalendarGenerator", project.Id.ToString("N"), "export-pair");
        Directory.CreateDirectory(tempDir);
        try
        {
            var pages = await RenderMonthPagesAsync(project, tempDir, progress, ct);

            progress?.Report("PDF (tiszta) összeállítása…");
            await WriteCleanPdfAsync(project, pages, cleanPath, progress, ct);

            progress?.Report("Előnézet kép összeállítása…");
            await Task.Run(() => WritePreviewStrip(pages, stripPath), ct);

            progress?.Report("Kész.");
            return (cleanPath, stripPath);
        }
        finally
        {
            TryDeleteDir(tempDir);
        }
    }

    private async Task<List<string>> RenderMonthPagesAsync(
        CalendarProject project,
        string tempDir,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        project.Paper.ApplyPreset();
        var pageImages = new List<string>();
        for (var month = 1; month <= 12; month++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Lap renderelése: {month}/12…");
            var model = _calendarEngine.BuildMonth(project, month);
            var png = Path.Combine(tempDir, $"month-{month:00}.png");
            await _pageRender.RenderMonthToPngAsync(project, model, png);
            pageImages.Add(png);
        }

        return pageImages;
    }

    private static async Task WriteCleanPdfAsync(
        CalendarProject project,
        IReadOnlyList<string> pageImages,
        string outputPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var widthPt = project.Paper.WidthMm / 25.4 * 72;
        var heightPt = project.Paper.HeightMm / 25.4 * 72;

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                foreach (var imagePath in pageImages)
                {
                    container.Page(page =>
                    {
                        page.Size(new PageSize((float)widthPt, (float)heightPt));
                        page.Margin(0);
                        page.Content().Image(imagePath).FitArea();
                    });
                }
            }).GeneratePdf(outputPath);
        }, ct);
    }

    private static void WritePreviewStrip(IReadOnlyList<string> pageImages, string outputPath)
    {
        if (pageImages.Count == 0)
            throw new InvalidOperationException("Nincs renderelt oldal az előnézet képhez.");

        var pages = pageImages.Select(LoadBitmap).ToList();

        var srcW = pages[0].PixelWidth;
        var srcH = pages[0].PixelHeight;
        var outW = Math.Max(1, (int)Math.Round(srcW * PreviewStripScale));
        var pageOutH = Math.Max(1, (int)Math.Round(srcH * PreviewStripScale));
        var outH = pageOutH * pages.Count;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            for (var i = 0; i < pages.Count; i++)
            {
                var rect = new Rect(0, i * pageOutH, outW, pageOutH);
                dc.DrawImage(pages[i], rect);
            }
        }

        var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var fs = File.Create(outputPath);
        encoder.Save(fs);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static void TryDeleteDir(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public static string BuildExportBaseName(string orderId, CalendarProject project)
    {
        project.Paper.ApplyPreset();
        var safeId = string.IsNullOrWhiteSpace(orderId)
            ? $"Naptar_{project.Year}"
            : SanitizeFileName(orderId);
        return $"{safeId}_{project.Paper.SizeLabel}_{project.Paper.OrientationLabel}";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
