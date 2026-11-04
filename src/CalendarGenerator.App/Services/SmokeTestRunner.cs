using System.IO;
using CalendarGenerator.Core.Calendar;
using CalendarGenerator.Core.Models;
using CalendarGenerator.Core.NameDays;

namespace CalendarGenerator.App.Services;

/// <summary>
/// Headless smoke checks invoked with --smoke-pdf.
/// </summary>
public static class SmokeTestRunner
{
    public static async Task<int> RunAsync()
    {
        try
        {
            var nameDays = new HungarianCatholicNameDayProvider();
            var engine = new CalendarEngine(nameDays);
            var pageRender = new PageRenderService();
            var pdf = new PdfExportService(pageRender, engine);

            var project = new CalendarProject
            {
                Year = 2024, // leap year
                Name = "Smoke"
            };
            project.Events.Add(new CustomEvent
            {
                Month = 2,
                Day = 29,
                Description = "Szökőnap"
            });
            project.Events.Add(new CustomEvent
            {
                Month = 5,
                Day = 15,
                Description = "Boti születésnapja"
            });
            project.Events.Add(new CustomEvent
            {
                Month = 5,
                Day = 15,
                Description = "Családi ebéd"
            });

            var feb = engine.BuildMonth(project, 2);
            if (feb.Cells.Count(c => c.Date is not null) != 29)
                throw new InvalidOperationException("Leap year February must have 29 days.");

            var may = engine.BuildMonth(project, 5);
            var day15 = may.Cells.Single(c => c.DayNumber == 15);
            if (day15.Events.Count != 2)
                throw new InvalidOperationException("Expected two events on May 15.");

            var multiNames = nameDays.GetNameDays(new DateOnly(2024, 6, 29), "hu");
            if (multiNames.Count < 2)
                throw new InvalidOperationException("Expected multiple name days on June 29.");

            // Render all 12 months at preview DPI to validate layout.
            for (var month = 1; month <= 12; month++)
            {
                var model = engine.BuildMonth(project, month);
                var bitmap = pageRender.RenderMonth(project, model, dpi: 96);
                if (bitmap.PixelWidth < 100 || bitmap.PixelHeight < 100)
                    throw new InvalidOperationException($"Invalid render for month {month}.");
            }

            var outPath = Path.Combine(Path.GetTempPath(), "CalendarGenerator", $"smoke-{Guid.NewGuid():N}.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            await pdf.ExportAsync(project, outPath);

            var info = new FileInfo(outPath);
            if (!info.Exists || info.Length < 10_000)
                throw new InvalidOperationException("PDF was not created or is too small.");

            // QuestPDF embeds 12 full-page images; a minimal sanity threshold for 12 A4 pages @300dpi.
            if (info.Length < 200_000)
                throw new InvalidOperationException($"PDF seems too small for 12 pages: {info.Length} bytes.");

            Console.WriteLine($"SMOKE OK pages=12 size={info.Length} path={outPath}");
            try { File.Delete(outPath); } catch { /* ignore */ }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SMOKE FAILED: " + ex);
            return 1;
        }
    }
}
