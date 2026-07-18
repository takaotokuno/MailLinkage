namespace MailBatch.Console.NotificationMails;

/// <summary>
/// 通知メール送信の操作を提供します。
/// </summary>
internal interface IMailNotifier
{
    /// <summary>
    /// 指定された宛先と内容で通知メールを送信します。
    /// </summary>
    Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定された複数の通知メールを送信します。
    /// </summary>
    Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default);
}
