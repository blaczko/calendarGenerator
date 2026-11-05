using System.IO;
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
        await ExportInternalAsync(project, outputPath, watermark: false, progress, ct);
    }

    /// <summary>
    /// Exports two PDFs into <paramref name="outputFolder"/>:
    /// clean: {orderId}.pdf and watermarked: {orderId}_watermarked.pdf
    /// </summary>
    public async Task<(string CleanPath, string WatermarkedPath)> ExportOrderPairAsync(
        CalendarProject project,
        string outputFolder,
        string orderId,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);
        var safeId = string.IsNullOrWhiteSpace(orderId) ? $"Naptar_{project.Year}" : SanitizeFileName(orderId);
        var cleanPath = Path.Combine(outputFolder, $"{safeId}.pdf");
        var wmPath = Path.Combine(outputFolder, $"{safeId}_watermarked.pdf");

        progress?.Report("PDF (tiszta) renderelése…");
        await ExportInternalAsync(project, cleanPath, watermark: false, progress, ct);
        progress?.Report("PDF (vízjel) renderelése…");
        await ExportInternalAsync(project, wmPath, watermark: true, progress, ct);
        progress?.Report("Kész.");
        return (cleanPath, wmPath);
    }

    private async Task ExportInternalAsync(
        CalendarProject project,
        string outputPath,
        bool watermark,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        project.Paper.ApplyPreset();
        var tempDir = Path.Combine(Path.GetTempPath(), "CalendarGenerator", project.Id.ToString("N"),
            watermark ? "wm" : "clean");
        Directory.CreateDirectory(tempDir);

        var pageImages = new List<string>();
        string? logoPath = null;
        try
        {
            if (watermark)
            {
                progress?.Report("Vízjel logó előkészítése…");
                logoPath = WatermarkLogoService.EnsureTransparentLogoPng();
            }

            for (var month = 1; month <= 12; month++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Lap renderelése: {month}/12…");
                var model = _calendarEngine.BuildMonth(project, month);
                var png = Path.Combine(tempDir, $"month-{month:00}.png");
                await _pageRender.RenderMonthToPngAsync(project, model, png);
                pageImages.Add(png);
            }

            progress?.Report(watermark ? "Vízjeles PDF összeállítása…" : "PDF összeállítása…");
            var widthPt = project.Paper.WidthMm / 25.4 * 72;
            var heightPt = project.Paper.HeightMm / 25.4 * 72;
            var logo = logoPath;

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
                            page.Content().Layers(layers =>
                            {
                                layers.PrimaryLayer().Image(imagePath).FitArea();
                                if (watermark && logo is not null)
                                {
                                    layers.Layer().AlignCenter().AlignMiddle().Width(260).Height(140)
                                        .Image(logo).FitArea();
                                }
                            });
                        });
                    }
                }).GeneratePdf(outputPath);
            }, ct);
        }
        finally
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
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
