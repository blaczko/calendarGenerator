using System.IO;
using System.Text.Json;
using CalendarGenerator.Core.Settings;

namespace CalendarGenerator.App.Services;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string SettingsPath { get; }

    public AppSettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatholicCalendarCreator");
        Directory.CreateDirectory(dir);
        SettingsPath = Path.Combine(dir, "app-settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return CreateDefaults();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefaults();
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static AppSettings CreateDefaults()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var root = Path.Combine(docs, "CatholicCalendarCreator");
        return new AppSettings
        {
            OrdersSourceFolder = Path.Combine(root, "Orders"),
            PdfOutputFolder = Path.Combine(root, "PdfExport"),
            GoogleDriveRootFolderId = "15rbvA2819MJelitvQZzpYxlcrt_7VsZN",
            GoogleDriveRootFolderName = "Naptar"
        };
    }
}
