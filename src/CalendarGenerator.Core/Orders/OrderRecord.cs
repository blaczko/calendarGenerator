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
    /// <summary>Paper size from the order form: A4, A3, or A5 (default A4).</summary>
    public string PaperSize { get; set; } = "A4";
    /// <summary>Print finish: fenyes (glossy) or matt (default fenyes).</summary>
    public string PrintFinish { get; set; } = "fenyes";
    public string Comments { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW";
    public bool PrivacyConsent { get; set; }

    /// <summary>True when the order was started from the office walk-in URL (?officeorder=true).</summary>
    public bool DirectFromOffice { get; set; }

    /// <summary>Phone was confirmed reachable on WhatsApp (or manually marked).</summary>
    public bool WhatsAppVerified { get; set; }

    /// <summary>Normalized WhatsApp digits that were verified (clears if phone changes).</summary>
    public string WhatsAppVerifiedPhone { get; set; } = string.Empty;

    /// <summary>Welcome WhatsApp message was sent (or manually marked).</summary>
    public bool WhatsAppWelcomeSent { get; set; }

    /// <summary>Review / látványterv WhatsApp message was sent (or manually marked).</summary>
    public bool WhatsAppReviewSent { get; set; }

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
