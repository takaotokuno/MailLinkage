using MailBatch.Console.Options;

namespace MailBatch.Console.ReceivedMails;

internal static class MailSearchConditionFactory
{
    /// <summary>
    /// メール検索設定から、対象メッセージを絞り込むアプリケーション層の検索条件を作成します。
    /// </summary>
    public static MailSearchCondition Create(MailSearchOptions options)
    {
        DateTime? deliveredAfter = options.SinceDays is > 0
            ? DateTime.UtcNow.Date.AddDays(-options.SinceDays.Value)
            : null;

        return new MailSearchCondition(
            string.IsNullOrWhiteSpace(options.SubjectContains) ? null : options.SubjectContains,
            string.IsNullOrWhiteSpace(options.From) ? null : options.From,
            deliveredAfter);
    }
}
