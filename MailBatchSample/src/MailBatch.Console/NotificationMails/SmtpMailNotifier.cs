using MailBatch.Console.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MailBatch.Console.NotificationMails;

/// <summary>
/// SMTPを使用して通知メールを送信します。
/// </summary>
internal sealed class SmtpMailNotifier(
    MailNotificationOptions notificationOptions,
    ILogger<SmtpMailNotifier> logger) : IMailNotifier
{
    /// <summary>
    /// SMTPサーバー経由で、指定された宛先へ通知メールを送信します。
    /// </summary>
    public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default) => SendAsync([notification], cancellationToken);

    /// <summary>
    /// SMTPサーバー経由で、指定された複数の通知メールを送信します。
    /// </summary>
    public async Task SendAsync(
        IReadOnlyCollection<MailNotification> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return;
        }

        using SmtpClient smtpClient = new();
        await smtpClient.ConnectAsync(
            notificationOptions.SmtpHost,
            notificationOptions.SmtpPort,
            SecureSocketOptions.SslOnConnect,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(notificationOptions.UserName))
        {
            await smtpClient.AuthenticateAsync(
                notificationOptions.UserName,
                notificationOptions.Password!,
                cancellationToken);
        }

        foreach (MailNotification notification in notifications)
        {
            MimeMessage message = CreateMessage(notification);
            _ = await smtpClient.SendAsync(message, cancellationToken);

            logger.LogInformation(
                "Notification mail sent. To={NotificationTo}",
                notification.To);
        }

        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    /// <summary>
    /// API連携用のメッセージ本文を作成します。
    /// </summary>
    private MimeMessage CreateMessage(MailNotification notification)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(notificationOptions.From));
        message.To.Add(MailboxAddress.Parse(notification.To));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain") { Text = notification.Body };
        message.Date = DateTimeOffset.UtcNow;

        return message;
    }
}
