using System.Text.RegularExpressions;

namespace CalendarGenerator.Core.Orders;

public static class PhoneNumberHelper
{
    /// <summary>
    /// Normalizes a Romanian (or already international) phone number to WhatsApp digits
    /// (country code + national number, no plus / spaces), e.g. 40712345678.
    /// </summary>
    public static bool TryNormalizeForWhatsApp(string? raw, out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var cleaned = Regex.Replace(raw.Trim(), @"[\s\-().]", string.Empty);
        if (cleaned.StartsWith("00", StringComparison.Ordinal))
            cleaned = "+" + cleaned[2..];

        if (cleaned.StartsWith('+'))
        {
            var intl = DigitsOnly(cleaned[1..]);
            if (intl.Length is >= 10 and <= 15)
            {
                digits = intl;
                return true;
            }
            return false;
        }

        var local = DigitsOnly(cleaned);
        // Romanian national: 07xxxxxxxx / 02xxxxxxxx / 03xxxxxxxx
        if (Regex.IsMatch(local, @"^0[237]\d{8}$"))
        {
            digits = "40" + local[1..];
            return true;
        }

        // Already missing leading 0 but has RO country code
        if (Regex.IsMatch(local, @"^40[237]\d{8}$"))
        {
            digits = local;
            return true;
        }

        // Generic international without +
        if (local.Length is >= 10 and <= 15 && !local.StartsWith('0'))
        {
            digits = local;
            return true;
        }

        return false;
    }

    public static string FormatDisplay(string digits) =>
        string.IsNullOrEmpty(digits) ? string.Empty : "+" + digits;

    private static string DigitsOnly(string value) =>
        Regex.Replace(value, @"\D", string.Empty);
}
