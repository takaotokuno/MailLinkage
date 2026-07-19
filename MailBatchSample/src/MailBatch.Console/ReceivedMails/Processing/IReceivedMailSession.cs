namespace MailBatch.Console.ReceivedMails.Processing;

/// <summary>
/// 受信メールセッションのライフサイクルとメール取得を提供します。
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
    /// 指定された受信メールIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}
