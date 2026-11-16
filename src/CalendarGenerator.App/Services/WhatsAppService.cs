using System.Diagnostics;
using CalendarGenerator.Core.Orders;

namespace CalendarGenerator.App.Services;

/// <summary>
/// Free WhatsApp integration via official click-to-chat links (wa.me).
/// Opens the installed WhatsApp Desktop / Web — no Business API required.
/// </summary>
public static class WhatsAppService
{
    /// <summary>Public web-app URL used for customer review links in WhatsApp messages.</summary>
    public const string ReviewWebAppBaseUrl =
        "https://script.google.com/macros/s/AKfycbwx0WrFHBjckGTiAsfqPCZbgBfCyPQeT64Xv1cuoC7Tz-oc0RlEn-h6I6j0-hnrOSwa/exec";

    /// <summary>
    /// Short GitHub Pages landing that lets the customer pick a Google account slot
    /// before opening Apps Script (avoids WhatsApp/WebView + /macros/u/N/ breakage).
    /// </summary>
    public const string ReviewLandingBaseUrl =
        "https://naptar-web.pages.dev/";

    public static bool TryBuildChatUrl(string? phone, string? message, out Uri url, out string error)
    {
        url = null!;
        error = string.Empty;

        if (!PhoneNumberHelper.TryNormalizeForWhatsApp(phone, out var digits))
        {
            error = "Érvénytelen telefonszám. Használjon pl. +40 712 345 678 vagy 0712 345 678 formátumot.";
            return false;
        }

        var builder = new UriBuilder("https://wa.me/" + digits);
        if (!string.IsNullOrWhiteSpace(message))
            builder.Query = "text=" + Uri.EscapeDataString(message.Trim());

        url = builder.Uri;
        return true;
    }

    public static bool TryOpenChat(string? phone, string? message, out string error)
    {
        if (!TryBuildChatUrl(phone, message, out var url, out error))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url.AbsoluteUri,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            error = "Nem sikerült megnyitni a WhatsAppot: " + ex.Message;
            return false;
        }
    }

    public static string BuildReviewUrl(string? orderId, bool withAccountChooser = true)
    {
        var id = string.IsNullOrWhiteSpace(orderId) ? string.Empty : orderId.Trim();
        // Prefer short Pages landing: WhatsApp often breaks long accounts.google.com links,
        // and AccountChooser alone does not always stop /macros/u/N/ rewrites.
        if (withAccountChooser)
        {
            return $"{ReviewLandingBaseUrl}?page=review&orderId={Uri.EscapeDataString(id)}";
        }

        return $"{ReviewWebAppBaseUrl}?page=review&orderId={Uri.EscapeDataString(id)}";
    }

    /// <summary>
    /// Opens Google's account picker, then continues to the Apps Script URL.
    /// Avoids the broken /macros/u/N/ redirect when several Google accounts are signed in.
    /// </summary>
    public static string WithGoogleAccountChooser(string targetUrl)
    {
        var clean = NormalizeAppsScriptUrl(targetUrl);
        return "https://accounts.google.com/AccountChooser?continue=" + Uri.EscapeDataString(clean);
    }

    public static string NormalizeAppsScriptUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(
            url.Trim(),
            @"/macros/u/\d+/",
            "/macros/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public enum MessageKind
    {
        Welcome,
        Review
    }

    public static string BuildDefaultMessage(OrderRecord order) => BuildWelcomeMessage(order);

    public static string BuildWelcomeMessage(OrderRecord order)
    {
        var name = ClientName(order);
        var size = string.Equals(order.PaperSize, "A3", StringComparison.OrdinalIgnoreCase) ? "A3"
            : string.Equals(order.PaperSize, "A5", StringComparison.OrdinalIgnoreCase) ? "A5 asztali"
            : "A4";
        var finish = string.Equals(order.PrintFinish, "matt", StringComparison.OrdinalIgnoreCase)
            ? "Matt"
            : "Fényes";
        var orientation = string.Equals(order.CalendarFormatPref, "landscape", StringComparison.OrdinalIgnoreCase)
            ? "Fekvő"
            : "Álló";
        var photoCount = order.Photos?.Count ?? 0;
        var eventCount = order.Events?.Count ?? 0;
        var reviewUrl = BuildReviewUrl(order.OrderId);

        return
            $"Kedves {name},\n\n" +
            "Köszönjük szépen a rendelését.\n" +
            $"Termék neve: Naptár {size}\n" +
            $"Felület: {finish}\n" +
            $"Tájolás: {orientation}\n" +
            $"Feltöltött képek száma: {photoCount}\n" +
            $"Családi események száma: {eventCount}\n\n" +
            "A rendelés kiegészítéséhez kérem használja a következő linket:\n" +
            $"{reviewUrl}\n" +
            "Itt feltölthet / változtathat a felhasználandó képeken valamint családi eseményeken.\n\n" +
            "Bármi kérdése van, forduljon hozzánk bizalommal.\n\n" +
            "Üdvözlettel,\n" +
            "Zátai Pixel";
    }

    public static string BuildReviewMessage(OrderRecord order)
    {
        var name = ClientName(order);
        var reviewUrl = BuildReviewUrl(order.OrderId);
        return
            $"Kedves {name},\n\n" +
            "Elkészült a rendelt naptár látványterve. Megnézheti az alábbi link megnyitásával:\n" +
            $"{reviewUrl}\n\n" +
            "Amennyiben bármi módosítást szeretne, kérem írjon nyugodtan.\n\n" +
            "Köszönettel,\n" +
            "Zátai Pixel";
    }

    public static string BuildMessage(OrderRecord order, MessageKind kind) =>
        kind == MessageKind.Review ? BuildReviewMessage(order) : BuildWelcomeMessage(order);

    private static string ClientName(OrderRecord order) =>
        string.IsNullOrWhiteSpace(order.FullName) ? "Ügyfél" : order.FullName.Trim();
}
