using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Searching;

namespace MailBatch.Console.ReceivedMails.Processing;

/// <summary>
/// 受信メールフォルダに対する操作を提供します。
/// </summary>
internal interface IReceivedMailSession : IAsyncDisposable
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
    /// 検索条件に一致する処理対象メールの受信メールID一覧を取得します。
    /// </summary>
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定された受信メールIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    Task<ReceivedMailContent> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, string messageId, CancellationToken cancellationToken = default);
}
