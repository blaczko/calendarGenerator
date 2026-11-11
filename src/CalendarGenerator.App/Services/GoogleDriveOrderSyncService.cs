using System.IO;
using System.Security.Cryptography;
using CalendarGenerator.Core.Settings;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CalendarGenerator.App.Services;

public sealed class GoogleDriveOrderSyncService
{
    /// <summary>Full Drive scope so preview strips can be uploaded into order folders.</summary>
    private static readonly string[] Scopes = [DriveService.Scope.Drive];
    private static readonly string RequiredScopeFingerprint = string.Join(' ', Scopes);
    private readonly string _tokenFolder;
    private readonly string _scopeMarkerPath;

    public GoogleDriveOrderSyncService()
    {
        _tokenFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatholicCalendarCreator",
            "google-token");
        Directory.CreateDirectory(_tokenFolder);
        _scopeMarkerPath = Path.Combine(_tokenFolder, "scopes.txt");
    }

    public async Task<bool> ConnectAsync(AppSettings settings, CancellationToken ct = default)
    {
        // Always re-consent so write scope is granted after upgrades from DriveReadonly.
        ClearTokenStore();
        _ = await AuthorizeAsync(settings, ct);
        settings.GoogleDriveConnected = true;
        return true;
    }

    public void Disconnect(AppSettings settings)
    {
        settings.GoogleDriveConnected = false;
        ClearTokenStore();
    }

    private void ClearTokenStore()
    {
        try
        {
            if (Directory.Exists(_tokenFolder))
                Directory.Delete(_tokenFolder, recursive: true);
            Directory.CreateDirectory(_tokenFolder);
        }
        catch
        {
            // ignore
        }
    }

    private void EnsureScopeMarkerOrClear()
    {
        try
        {
            if (File.Exists(_scopeMarkerPath))
            {
                var previous = File.ReadAllText(_scopeMarkerPath).Trim();
                if (string.Equals(previous, RequiredScopeFingerprint, StringComparison.Ordinal))
                    return;
            }

            ClearTokenStore();
            File.WriteAllText(_scopeMarkerPath, RequiredScopeFingerprint);
        }
        catch
        {
            ClearTokenStore();
        }
    }

    private static bool HasRequiredWriteScope(UserCredential credential)
    {
        var scope = credential.Token?.Scope;
        if (string.IsNullOrWhiteSpace(scope))
            return false;

        // Exact scope tokens — "drive.readonly" must not count as write.
        var parts = scope.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Any(p =>
            p.Equals(DriveService.Scope.Drive, StringComparison.OrdinalIgnoreCase) ||
            p.Equals(DriveService.Scope.DriveFile, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInsufficientScopeError(Exception ex)
    {
        var text = ex.ToString();
        return text.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.OrdinalIgnoreCase)
               || (text.Contains("Forbidden", StringComparison.OrdinalIgnoreCase)
                   && text.Contains("scope", StringComparison.OrdinalIgnoreCase));
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
        var skippedFiles = 0;

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
                progress?.Report($"Szinkron: {yearName}/{orderName}…");
                var localOrder = Path.Combine(localYear, orderName);
                var (downloaded, skipped) = await DownloadFolderRecursiveAsync(
                    service, orderFolder.Id, localOrder, progress, ct);
                downloadedFiles += downloaded;
                skippedFiles += skipped;
            }
        }

        progress?.Report(
            downloadedFiles == 0
                ? $"Kész. Nincs új fájl ({skippedFiles} már megvolt)."
                : $"Kész. Új/frissített: {downloadedFiles}, kihagyva: {skippedFiles}.");
        return downloadedFiles;
    }

    /// <summary>
    /// Uploads (or replaces) preview_strip.jpg under the order's 00_Megrendelo_adatai folder
    /// and shares it with anyone who has the link (for the customer review web page).
    /// </summary>
    public async Task<string> UploadPreviewStripAsync(
        AppSettings settings,
        string driveFolderId,
        string localJpegPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(driveFolderId))
            throw new InvalidOperationException("Hiányzik a Drive mappa azonosító.");
        if (!File.Exists(localJpegPath))
            throw new FileNotFoundException("Az előnézet kép nem található.", localJpegPath);

        try
        {
            return await UploadPreviewStripCoreAsync(settings, driveFolderId, localJpegPath, progress, ct);
        }
        catch (Exception ex) when (IsInsufficientScopeError(ex))
        {
            // Old readonly token — force browser consent for write scope, then retry once.
            progress?.Report("Drive jogosultság frissítése (írás)…");
            ClearTokenStore();
            return await UploadPreviewStripCoreAsync(settings, driveFolderId, localJpegPath, progress, ct);
        }
    }

    private async Task<string> UploadPreviewStripCoreAsync(
        AppSettings settings,
        string driveFolderId,
        string localJpegPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var service = await CreateDriveServiceAsync(settings, ct);
        progress?.Report("Előnézet feltöltése a Drive-ra…");

        var metaFolderId = await EnsureChildFolderAsync(service, driveFolderId, "00_Megrendelo_adatai", ct);
        var existing = await FindChildFileAsync(service, metaFolderId, "preview_strip.jpg", ct);

        string fileId;
        await using (var stream = new FileStream(localJpegPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (existing is not null)
            {
                var update = service.Files.Update(
                    new Google.Apis.Drive.v3.Data.File(),
                    existing.Id,
                    stream,
                    "image/jpeg");
                update.Fields = "id";
                update.SupportsAllDrives = true;
                var updated = await update.UploadAsync(ct);
                if (updated.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    var msg = updated.Exception?.Message ?? updated.Status.ToString();
                    throw new InvalidOperationException($"Drive feltöltés sikertelen: {msg}", updated.Exception);
                }

                fileId = existing.Id;
            }
            else
            {
                var meta = new Google.Apis.Drive.v3.Data.File
                {
                    Name = "preview_strip.jpg",
                    Parents = [metaFolderId]
                };
                var create = service.Files.Create(meta, stream, "image/jpeg");
                create.Fields = "id";
                create.SupportsAllDrives = true;
                var created = await create.UploadAsync(ct);
                if (created.Status != Google.Apis.Upload.UploadStatus.Completed)
                {
                    var msg = created.Exception?.Message ?? created.Status.ToString();
                    throw new InvalidOperationException($"Drive feltöltés sikertelen: {msg}", created.Exception);
                }

                fileId = create.ResponseBody?.Id
                    ?? throw new InvalidOperationException("Drive nem adott vissza fájlazonosítót.");
            }
        }

        await EnsureAnyoneWithLinkAsync(service, fileId, ct);
        progress?.Report("Előnézet feltöltve.");
        return fileId;
    }

    private static async Task<string> EnsureChildFolderAsync(
        DriveService service,
        string parentId,
        string name,
        CancellationToken ct)
    {
        var folders = await ListChildrenAsync(service, parentId, foldersOnly: true, ct);
        var existing = folders.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing?.Id is not null)
            return existing.Id;

        var created = await service.Files.Create(new Google.Apis.Drive.v3.Data.File
        {
            Name = name,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [parentId]
        }).ExecuteAsync(ct);

        return created.Id
            ?? throw new InvalidOperationException($"Nem sikerült létrehozni a mappát: {name}");
    }

    private static async Task<Google.Apis.Drive.v3.Data.File?> FindChildFileAsync(
        DriveService service,
        string parentId,
        string fileName,
        CancellationToken ct)
    {
        var files = await ListChildrenAsync(service, parentId, foldersOnly: false, ct);
        return files.FirstOrDefault(f =>
            f.MimeType != "application/vnd.google-apps.folder" &&
            string.Equals(f.Name, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task EnsureAnyoneWithLinkAsync(DriveService service, string fileId, CancellationToken ct)
    {
        try
        {
            var permission = new Google.Apis.Drive.v3.Data.Permission
            {
                Type = "anyone",
                Role = "reader",
                AllowFileDiscovery = false
            };
            var request = service.Permissions.Create(permission, fileId);
            request.SupportsAllDrives = true;
            await request.ExecuteAsync(ct);
        }
        catch
        {
            // Already shared or insufficient permission — review page still works for the owner.
        }
    }

    private async Task<UserCredential> AuthorizeAsync(AppSettings settings, CancellationToken ct)
    {
        var clientId = CleanCredential(settings.GoogleClientId);
        var clientSecret = CleanCredential(settings.GoogleClientSecret);
        settings.GoogleClientId = clientId;
        settings.GoogleClientSecret = clientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Adja meg a Google OAuth Client ID és Client Secret értékeket a beállításokban " +
                "(Google Cloud Console → OAuth 2.0 Client ID, típus: Desktop).");
        }

        if (!clientId.EndsWith(".apps.googleusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A Client ID formátuma hibás. Teljes értéket várunk, pl. " +
                "123456789-xxxx.apps.googleusercontent.com (nem a megjelenített név).");
        }

        // Newer Google secrets: GOCSPX- + exactly 28 chars = 35 total.
        if (clientSecret.Contains('…') || clientSecret.Contains("****") ||
            (clientSecret.StartsWith("GOCSPX-", StringComparison.Ordinal) && clientSecret.Length != 35) ||
            clientSecret.Length < 16)
        {
            throw new InvalidOperationException(
                $"A Client Secret hiányosnak tűnik (most {clientSecret.Length} karakter). " +
                "A GOCSPX- titkok hossza pontosan 35. A Console csak létrehozáskor / resetkor mutatja a teljes értéket — " +
                "ne a maszkolt (****) megjelenést másolja. Nyissa meg a Desktop klienst → Reset secret → másolja az újat.");
        }

        EnsureScopeMarkerOrClear();

        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        try
        {
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "calendar-generator-user",
                ct,
                new FileDataStore(_tokenFolder, true));

            if (!HasRequiredWriteScope(credential))
            {
                ClearTokenStore();
                File.WriteAllText(_scopeMarkerPath, RequiredScopeFingerprint);
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets,
                    Scopes,
                    "calendar-generator-user",
                    ct,
                    new FileDataStore(_tokenFolder, true));
            }

            try { File.WriteAllText(_scopeMarkerPath, RequiredScopeFingerprint); }
            catch { /* ignore */ }

            return credential;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{ex.Message}\n\nHasznált Client ID: {clientId}\n" +
                $"Secret hossz: {clientSecret.Length} karakter (prefix: {clientSecret[..Math.Min(7, clientSecret.Length)]}…).\n" +
                "Ha a Client ID helyes a böngésző URL-ben is (client_id=...), akkor a secret rossz vagy nem ehhez a klienshez tartozik.",
                ex);
        }
    }

    private static string CleanCredential(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var s = value.Trim().Trim('"', '\'', '“', '”', '„');
        // Paste often inserts spaces/newlines; OAuth IDs/secrets never contain whitespace.
        return string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
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
            request.Fields = foldersOnly
                ? "nextPageToken, files(id, name, mimeType)"
                : "nextPageToken, files(id, name, mimeType, md5Checksum, size, modifiedTime)";
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

    private static async Task<(int Downloaded, int Skipped)> DownloadFolderRecursiveAsync(
        DriveService service,
        string folderId,
        string localPath,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(localPath);
        var downloaded = 0;
        var skipped = 0;
        var children = await ListChildrenAsync(service, folderId, foldersOnly: false, ct);
        foreach (var child in children)
        {
            ct.ThrowIfCancellationRequested();
            if (child.MimeType == "application/vnd.google-apps.folder")
            {
                var sub = Path.Combine(localPath, Sanitize(child.Name ?? child.Id));
                var nested = await DownloadFolderRecursiveAsync(service, child.Id, sub, progress, ct);
                downloaded += nested.Downloaded;
                skipped += nested.Skipped;
                continue;
            }

            if (child.MimeType?.StartsWith("application/vnd.google-apps.", StringComparison.Ordinal) == true)
                continue; // skip native Docs/Sheets export complexity for now

            var target = Path.Combine(localPath, Sanitize(child.Name ?? child.Id));
            var forceRefresh = IsOrderMetadataJson(child.Name);
            if (!forceRefresh && IsLocalFileUpToDate(target, child))
            {
                skipped++;
                continue;
            }

            progress?.Report(forceRefresh
                ? $"Megrendelés JSON frissítése: {Path.GetFileName(target)}"
                : $"Új fájl: {Path.GetFileName(target)}");
            await using (var stream = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await service.Files.Get(child.Id).DownloadAsync(stream, ct);
            }

            // Preserve Drive modified time locally for faster next-pass checks.
            if (child.ModifiedTimeDateTimeOffset is { } modified)
            {
                try { File.SetLastWriteTimeUtc(target, modified.UtcDateTime); }
                catch { /* ignore */ }
            }

            downloaded++;
        }

        return (downloaded, skipped);
    }

    /// <summary>
    /// Order metadata must always be re-pulled — photos can skip via MD5, but events/status live here.
    /// </summary>
    private static bool IsOrderMetadataJson(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var name = Path.GetFileName(fileName);
        return name.Equals("megrendeles.json", StringComparison.OrdinalIgnoreCase)
               || name.Equals("order.json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Skip download when the local file already matches Drive (size + MD5 when available).
    /// </summary>
    private static bool IsLocalFileUpToDate(string localPath, Google.Apis.Drive.v3.Data.File remote)
    {
        if (!File.Exists(localPath))
            return false;

        var info = new FileInfo(localPath);
        if (remote.Size is long remoteSize && info.Length != remoteSize)
            return false;

        if (!string.IsNullOrWhiteSpace(remote.Md5Checksum))
        {
            try
            {
                var localMd5 = ComputeMd5Hex(localPath);
                return string.Equals(localMd5, remote.Md5Checksum, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // No MD5 from Drive: same size + local mtime not older than remote → treat as present.
        if (remote.Size is null)
            return info.Length > 0;

        if (remote.ModifiedTimeDateTimeOffset is { } remoteModified)
            return info.LastWriteTimeUtc >= remoteModified.UtcDateTime.AddSeconds(-2);

        return true;
    }

    private static string ComputeMd5Hex(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = MD5.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}
