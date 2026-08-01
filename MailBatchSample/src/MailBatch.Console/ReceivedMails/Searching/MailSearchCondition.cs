using MailBatch.Console.Options;

namespace MailBatch.Console.ReceivedMails.Searching;

/// <summary>
/// アプリケーション層で扱う受信メール検索条件です。
/// </summary>
internal sealed record MailSearchCondition(
    string? SubjectContains,
    string? From,
    DateTimeOffset? DeliveredAfter)
{
    /// <summary>
    /// 検索オプションからメール検索条件を作成します。
    /// </summary>
    public static MailSearchCondition FromOptions(MailSearchOptions options, DateTimeOffset utcNow)
    {
        DateTimeOffset utc = utcNow.ToUniversalTime();
        DateTimeOffset utcDate = new(
            utc.Year,
            utc.Month,
            utc.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        DateTimeOffset? deliveredAfter = options.SinceDays is > 0
            ? utcDate.AddDays(-options.SinceDays.Value)
            : null;

        return new MailSearchCondition(
            options.SubjectContains,
            options.From,
            deliveredAfter);
    }
}
