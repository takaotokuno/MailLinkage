namespace MailBatch.Console.ReceivedMails.Processing;

/// <summary>
/// 処理結果に応じた受信メールの移動を提供します。
/// </summary>
internal interface IReceivedMailMover
{
    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// API連携に失敗したメールを設定されたエラーメールボックスへ移動します。
    /// </summary>
    Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}
