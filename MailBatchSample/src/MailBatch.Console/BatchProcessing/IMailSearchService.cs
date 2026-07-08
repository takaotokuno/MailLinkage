using MailKit;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// バッチ処理対象となる受信メールを検索します。
/// </summary>
internal interface IMailSearchService
{
    /// <summary>
    /// 設定された検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync();
}
