using System.Text.RegularExpressions;

namespace FinanceApp.API.Services;

public static class PhoneNumberHelper
{
    public static string? NormalizeSriLankanNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var cleaned = Regex.Replace(phone.Trim(), @"[\s()-]", "");

        // 0712345678 -> +94712345678
        if (Regex.IsMatch(cleaned, @"^0[1-9]\d{8}$"))
            return "+94" + cleaned[1..];

        // 94712345678 -> +94712345678
        if (Regex.IsMatch(cleaned, @"^94[1-9]\d{8}$"))
            return "+" + cleaned;

        // Already in international format
        if (Regex.IsMatch(cleaned, @"^\+94[1-9]\d{8}$"))
            return cleaned;

        return null;
    }
}