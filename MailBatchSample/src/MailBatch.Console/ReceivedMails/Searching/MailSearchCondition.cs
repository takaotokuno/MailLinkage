using MailBatch.Console.Options;

namespace MailBatch.Console.ReceivedMails.Searching;

/// <summary>
/// アプリケーション層で扱う受信メール検索条件です。
/// </summary>
internal sealed record MailSearchCondition(
    string? SubjectContains,
    string? From,
    DateTime? DeliveredAfter)
{
    public static MailSearchCondition FromOptions(
        MailSearchOptions options)
    {
        DateTime? deliveredAfter = options.SinceDays is > 0
            ? DateTime.UtcNow.Date.AddDays(-options.SinceDays.Value)
            : null;

        return new MailSearchCondition(
            Normalize(options.SubjectContains),
            Normalize(options.From),
            deliveredAfter);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
