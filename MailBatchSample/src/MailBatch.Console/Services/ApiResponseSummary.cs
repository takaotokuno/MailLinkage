using System.Text.RegularExpressions;

namespace MailBatch.Console.Services;

internal static class ApiResponseSummary
{
    public static string? ExtractSavedId(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        var match = Regex.Match(responseBody, "\"id\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string Summarize(string value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(value, "\\s+", " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
