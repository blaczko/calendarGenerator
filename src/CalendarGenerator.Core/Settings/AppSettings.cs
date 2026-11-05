namespace CalendarGenerator.Core.Settings;

public sealed class AppSettings
{
    public string OrdersSourceFolder { get; set; } = string.Empty;
    public string PdfOutputFolder { get; set; } = string.Empty;
    public string GoogleClientId { get; set; } = string.Empty;
    public string GoogleClientSecret { get; set; } = string.Empty;
    public string GoogleDriveRootFolderId { get; set; } = "15rbvA2819MJelitvQZzpYxlcrt_7VsZN";
    public string GoogleDriveRootFolderName { get; set; } = "Naptar";
    public bool GoogleDriveConnected { get; set; }
}
