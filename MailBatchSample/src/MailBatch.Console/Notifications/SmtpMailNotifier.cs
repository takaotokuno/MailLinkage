using MailBatch.Console.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MailBatch.Console.Notifications;

internal sealed class SmtpMailNotifier(
    AppOptions options,
    ILogger<SmtpMailNotifier> logger) : IMailNotifier
{
    /// <summary>
    /// SMTPサーバー経由で、指定された宛先へ通知メールを送信します。
    /// </summary>
    public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default)
    {
        return SendAsync([notification], cancellationToken);
    }

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
            options.Notification.SmtpHost,
            options.Notification.SmtpPort,
            options.Notification.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.Notification.UserName))
        {
            await smtpClient.AuthenticateAsync(
                options.Notification.UserName,
                options.Notification.Password,
                cancellationToken);
        }

        foreach (MailNotification notification in notifications)
        {
            MimeMessage message = CreateMessage(notification);
            await smtpClient.SendAsync(message, cancellationToken);

            logger.LogInformation(
                "Notification mail sent. To={NotificationTo}",
                notification.To);
        }

        await smtpClient.DisconnectAsync(true, cancellationToken);
    }

    private MimeMessage CreateMessage(MailNotification notification)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse(options.Notification.From));
        message.To.Add(MailboxAddress.Parse(notification.To));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain") { Text = notification.Body };
        message.Date = notification.SentAt;

        return message;
    }
}
