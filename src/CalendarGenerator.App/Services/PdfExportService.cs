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

    public async Task ExportAsync(CalendarProject project, string outputPath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        project.Paper.ApplyPreset();
        var tempDir = Path.Combine(Path.GetTempPath(), "CalendarGenerator", project.Id.ToString("N"));
        Directory.CreateDirectory(tempDir);

        var pageImages = new List<string>();
        try
        {
            for (var month = 1; month <= 12; month++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Lap renderelése: {month}/12…");
                var model = _calendarEngine.BuildMonth(project, month);
                var png = Path.Combine(tempDir, $"month-{month:00}.png");
                await _pageRender.RenderMonthToPngAsync(project, model, png);
                pageImages.Add(png);
            }

            progress?.Report("PDF összeállítása…");
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

            progress?.Report("Kész.");
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
}
