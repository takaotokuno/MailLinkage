namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// アプリケーション層で扱う受信メール検索条件です。
/// </summary>
internal sealed record MailSearchCondition(
    string? SubjectContains,
    string? From,
    DateTime? DeliveredAfter)
{
    /// <summary>
    /// 検索条件が指定されていない全件検索条件を表します。
    /// </summary>
    public static MailSearchCondition All { get; } = new(null, null, null);
}
