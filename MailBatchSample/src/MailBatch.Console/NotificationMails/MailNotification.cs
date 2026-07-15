namespace MailBatch.Console.NotificationMails;

/// <summary>
/// 送信する通知メールの宛先、件名、本文を表します。
/// </summary>
internal sealed record MailNotification(
    string To,
    string Subject,
    string Body);
