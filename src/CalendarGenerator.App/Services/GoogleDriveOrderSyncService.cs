using System.IO;
using CalendarGenerator.Core.Settings;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CalendarGenerator.App.Services;

public sealed class GoogleDriveOrderSyncService
{
    private static readonly string[] Scopes = [DriveService.Scope.DriveReadonly];
    private readonly string _tokenFolder;

    public GoogleDriveOrderSyncService()
    {
        _tokenFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatholicCalendarCreator",
            "google-token");
        Directory.CreateDirectory(_tokenFolder);
    }

    public async Task<bool> ConnectAsync(AppSettings settings, CancellationToken ct = default)
    {
        _ = await AuthorizeAsync(settings, ct);
        settings.GoogleDriveConnected = true;
        return true;
    }

    public void Disconnect(AppSettings settings)
    {
        settings.GoogleDriveConnected = false;
        try
        {
            if (Directory.Exists(_tokenFolder))
                Directory.Delete(_tokenFolder, recursive: true);
        }
        catch
        {
            // ignore
        }
    }

    public async Task<int> SyncOrdersAsync(
        AppSettings settings,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.OrdersSourceFolder))
            throw new InvalidOperationException("Állítsa be a forrásmappát a beállításokban.");
        if (string.IsNullOrWhiteSpace(settings.GoogleDriveRootFolderId))
            throw new InvalidOperationException("Hiányzik a Google Drive gyökérmappa ID.");

        Directory.CreateDirectory(settings.OrdersSourceFolder);
        var service = await CreateDriveServiceAsync(settings, ct);

        progress?.Report("Évmappák lekérése…");
        var yearFolders = await ListChildrenAsync(service, settings.GoogleDriveRootFolderId, foldersOnly: true, ct);
        var downloadedFiles = 0;

        foreach (var yearFolder in yearFolders)
        {
            ct.ThrowIfCancellationRequested();
            var yearName = yearFolder.Name ?? "unknown";
            var localYear = Path.Combine(settings.OrdersSourceFolder, yearName);
            Directory.CreateDirectory(localYear);

            var orderFolders = await ListChildrenAsync(service, yearFolder.Id, foldersOnly: true, ct);
            foreach (var orderFolder in orderFolders)
            {
                ct.ThrowIfCancellationRequested();
                var orderName = Sanitize(orderFolder.Name ?? orderFolder.Id);
                progress?.Report($"Letöltés: {yearName}/{orderName}…");
                var localOrder = Path.Combine(localYear, orderName);
                downloadedFiles += await DownloadFolderRecursiveAsync(
                    service, orderFolder.Id, localOrder, progress, ct);
            }
        }

        progress?.Report($"Kész. Letöltött fájlok: {downloadedFiles}");
        return downloadedFiles;
    }

    private async Task<UserCredential> AuthorizeAsync(AppSettings settings, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.GoogleClientId) ||
            string.IsNullOrWhiteSpace(settings.GoogleClientSecret))
        {
            throw new InvalidOperationException(
                "Adja meg a Google OAuth Client ID és Client Secret értékeket a beállításokban " +
                "(Google Cloud Console → OAuth 2.0 Client ID, típus: Desktop).");
        }

        var secrets = new ClientSecrets
        {
            ClientId = settings.GoogleClientId.Trim(),
            ClientSecret = settings.GoogleClientSecret.Trim()
        };

        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            Scopes,
            "calendar-generator-user",
            ct,
            new FileDataStore(_tokenFolder, true));
    }

    private async Task<DriveService> CreateDriveServiceAsync(AppSettings settings, CancellationToken ct)
    {
        var credential = await AuthorizeAsync(settings, ct);
        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "CatholicCalendarCreator"
        });
    }

    private static async Task<IList<Google.Apis.Drive.v3.Data.File>> ListChildrenAsync(
        DriveService service,
        string parentId,
        bool foldersOnly,
        CancellationToken ct)
    {
        var q = foldersOnly
            ? $"'{parentId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false"
            : $"'{parentId}' in parents and trashed = false";

        var result = new List<Google.Apis.Drive.v3.Data.File>();
        string? pageToken = null;
        do
        {
            var request = service.Files.List();
            request.Q = q;
            request.Fields = "nextPageToken, files(id, name, mimeType)";
            request.PageSize = 100;
            request.PageToken = pageToken;
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;
            var response = await request.ExecuteAsync(ct);
            if (response.Files is not null)
                result.AddRange(response.Files);
            pageToken = response.NextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return result;
    }

    private static async Task<int> DownloadFolderRecursiveAsync(
        DriveService service,
        string folderId,
        string localPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(localPath);
        var count = 0;
        var children = await ListChildrenAsync(service, folderId, foldersOnly: false, ct);
        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();
            if (child.MimeType == "application/vnd.google-apps.folder")
            {
                var sub = Path.Combine(localPath, Sanitize(child.Name ?? child.Id));
                count += await DownloadFolderRecursiveAsync(service, child.Id, sub, progress, ct);
                continue;
            }

            if (child.MimeType?.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) == true)
                continue; // skip native Docs/Sheets export complexity for now

            var target = Path.Combine(localPath, Sanitize(child.Name ?? child.Id));
            await using var stream = new FileStream(target, FileMode.Create, FileAccess.Write);
            await service.Files.Get(child.Id).DownloadAsync(stream, ct);
            count++;
        }

        return count;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
