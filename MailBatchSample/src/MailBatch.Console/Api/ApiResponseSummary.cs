using System.Text.RegularExpressions;

namespace MailBatch.Console.Api;

/// <summary>
/// API応答本文をログ出力向けに要約します。
/// </summary>
internal static class ApiResponseSummary
{
    private const int SAVED_ID_GROUP_INDEX = 1;
    private const int DEFAULT_MAX_SUMMARY_LENGTH = 500;

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
        return match.Success ? match.Groups[SAVED_ID_GROUP_INDEX].Value : null;
    }

    /// <summary>
    /// 文字列の空白を正規化し、指定された最大長に収まる概要文字列を作成します。
    /// </summary>
    public static string Summarize(string value, int maxLength = DEFAULT_MAX_SUMMARY_LENGTH)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = Regex.Replace(value, "\\s+", " ").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
    }
}
