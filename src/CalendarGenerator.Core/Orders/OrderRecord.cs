namespace CalendarGenerator.Core.Orders;

public sealed class OrderRecord
{
    public string OrderId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int CalendarYear { get; set; } = DateTime.Now.Year + 1;
    public string CalendarLanguage { get; set; } = "hu";
    public string CalendarFormatPref { get; set; } = "portrait";
    public string Comments { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW";
    public bool PrivacyConsent { get; set; }
    public string DriveFolderUrl { get; set; } = string.Empty;
    public string DriveFolderId { get; set; } = string.Empty;
    public string LocalFolderPath { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public List<OrderEvent> Events { get; set; } = [];
    public List<OrderPhoto> Photos { get; set; } = [];
}

public sealed class OrderEvent
{
    public string EventDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class OrderPhoto
{
    public string Id { get; set; } = string.Empty;
    public int Month { get; set; }
    public int SortOrder { get; set; }
    public string OriginalFilename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
