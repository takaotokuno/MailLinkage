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
    public static MailSearchCondition FromOptions(MailSearchOptions options, DateTime utcNow)
    {
        DateTime? deliveredAfter = options.SinceDays is > 0
            ? utcNow.Date.AddDays(-options.SinceDays.Value)
            : null;

        return new MailSearchCondition(
            options.SubjectContains,
            options.From,
            deliveredAfter);
    }
}
