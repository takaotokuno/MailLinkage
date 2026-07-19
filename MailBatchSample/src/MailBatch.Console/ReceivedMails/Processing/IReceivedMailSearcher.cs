using MailBatch.Console.ReceivedMails.Searching;

namespace MailBatch.Console.ReceivedMails.Processing;

/// <summary>
/// 処理対象となる受信メールの検索を提供します。
/// </summary>
internal interface IReceivedMailSearcher
{
    /// <summary>
    /// 検索条件に一致する処理対象メールの受信メールID一覧を取得します。
    /// </summary>
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default);
}
