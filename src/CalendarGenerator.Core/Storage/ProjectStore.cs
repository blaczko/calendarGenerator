using System.Text.Json;
using System.Text.Json.Serialization;
using CalendarGenerator.Core.Models;

namespace CalendarGenerator.Core.Storage;

public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ProjectsRoot { get; }

    public ProjectStore(string? root = null)
    {
        ProjectsRoot = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CatholicCalendarCreator",
            "Projects");
        Directory.CreateDirectory(ProjectsRoot);
    }

    public string GetProjectFolder(CalendarProject project)
    {
        var folder = Path.Combine(ProjectsRoot, project.Id.ToString("N"));
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "photos"));
        Directory.CreateDirectory(Path.Combine(folder, "templates"));
        return folder;
    }

    public async Task<string> SaveAsync(CalendarProject project, CancellationToken ct = default)
    {
        project.Paper.ApplyPreset();
        project.ModifiedAt = DateTimeOffset.Now;

        var folder = GetProjectFolder(project);
        await CopyPhotosAsync(project, folder, ct);
        await CopyTemplateImagesAsync(project, folder, ct);
        await CopyThemeImagesAsync(project, folder, ct);

        var path = Path.Combine(folder, "project.json");
        project.FilePath = path;

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, JsonOptions, ct);
        return path;
    }

    public async Task<CalendarProject?> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<CalendarProject>(stream, JsonOptions, ct);
        if (project is not null)
            project.FilePath = path;
        return project;
    }

    public IReadOnlyList<string> ListProjectFiles()
    {
        if (!Directory.Exists(ProjectsRoot))
            return Array.Empty<string>();

        return Directory.GetDirectories(ProjectsRoot)
            .Select(d => Path.Combine(d, "project.json"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    private static async Task CopyPhotosAsync(CalendarProject project, string folder, CancellationToken ct)
    {
        var photosDir = Path.Combine(folder, "photos");
        foreach (var month in project.Months)
        {
            foreach (var photo in month.Photos)
            {
                if (string.IsNullOrWhiteSpace(photo.SourcePath) || !File.Exists(photo.SourcePath))
                    continue;

                var dest = Path.Combine(photosDir, $"{photo.Id:N}{Path.GetExtension(photo.SourcePath)}");
                if (!string.Equals(photo.SourcePath, dest, StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(() => File.Copy(photo.SourcePath, dest, overwrite: true), ct);
                    photo.ProjectCopyPath = dest;
                }
            }
        }
    }

    private static async Task CopyTemplateImagesAsync(CalendarProject project, string folder, CancellationToken ct)
    {
        var templatesDir = Path.Combine(folder, "templates");
        Directory.CreateDirectory(templatesDir);
        foreach (var template in project.CustomTemplates)
        {
            var source = !string.IsNullOrWhiteSpace(template.BackgroundImagePath) && File.Exists(template.BackgroundImagePath)
                ? template.BackgroundImagePath
                : template.ProjectCopyPath;

            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                continue;

            var dest = Path.Combine(templatesDir, $"{template.Id}{Path.GetExtension(source)}");
            if (!string.Equals(source, dest, StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => File.Copy(source, dest, overwrite: true), ct);
                template.ProjectCopyPath = dest;
                template.BackgroundImagePath = dest;
            }
        }
    }

    private static async Task CopyThemeImagesAsync(CalendarProject project, string folder, CancellationToken ct)
    {
        var templatesDir = Path.Combine(folder, "templates");
        Directory.CreateDirectory(templatesDir);
        foreach (var month in project.Months)
        {
            var theme = month.Theme;
            if (theme is null) continue;

            var source = !string.IsNullOrWhiteSpace(theme.BackgroundImagePath) && File.Exists(theme.BackgroundImagePath)
                ? theme.BackgroundImagePath
                : theme.ProjectCopyPath;

            if (string.IsNullOrWhiteSpace(source) || !File.Exists(source))
                continue;

            var dest = Path.Combine(templatesDir, $"month-{month.Month:00}{Path.GetExtension(source)}");
            if (!string.Equals(source, dest, StringComparison.OrdinalIgnoreCase))
            {
                await Task.Run(() => File.Copy(source, dest, overwrite: true), ct);
                theme.ProjectCopyPath = dest;
                theme.BackgroundImagePath = dest;
            }
        }
    }
}
