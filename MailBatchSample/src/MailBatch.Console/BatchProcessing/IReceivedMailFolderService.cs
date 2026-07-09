using MailBatch.Console.ReceivedMails;
using MailKit;
using MailKit.Search;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 受信メールフォルダに対する操作を提供します。
/// </summary>
internal interface IReceivedMailFolderService : IAsyncDisposable
{
    /// <summary>
    /// メールサーバーへ接続し、受信メールフォルダと処理済みフォルダを利用可能な状態にします。
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// メールサーバーから正常に切断します。
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(SearchQuery query, int maxMessages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定されたUIDのメール本文と内部受信日時を取得します。
    /// </summary>
    Task<ReceivedMailContent> ReadMessageAsync(UniqueId uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    Task MoveToProcessedMailboxAsync(UniqueId uid, string messageId, CancellationToken cancellationToken = default);
}
