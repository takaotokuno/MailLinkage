using System.Text.RegularExpressions;

namespace MailBatch.Console.Models;

internal static class ApiResponseSummary
{
    /// <summary>
    /// APIレスポンス本文から保存済みIDを抽出します。
    /// </summary>
    public static string? ExtractSavedId(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        Match match = Regex.Match(responseBody, "\"id\"\\s*:\\s*(\\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// 文字列の空白を正規化し、指定された最大長に収まる概要文字列を作成します。
    /// </summary>
    public static string Summarize(string value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = Regex.Replace(value, "\\s+", " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
