using System.Net;
using System.Text.RegularExpressions;
using MailBatch.Console.Models;
using MimeKit;

namespace MailBatch.Console.Mail;

internal static class ReceivedMailMapper
{
    public static ReceivedMailRequest ToRequest(MimeMessage message, DateTimeOffset? internalDate)
    {
        var receivedAt = internalDate?.ToUniversalTime()
            ?? (message.Date != DateTimeOffset.MinValue ? message.Date.ToUniversalTime() : DateTimeOffset.UtcNow);

        return new ReceivedMailRequest(
            MessageId: GetMessageId(message),
            Sender: message.From.Mailboxes.FirstOrDefault()?.Address ?? message.From.ToString(),
            Subject: message.Subject ?? string.Empty,
            Body: ExtractBody(message),
            ReceivedAt: receivedAt);
    }

    private static string GetMessageId(MimeMessage message)
    {
        var messageId = message.Headers[HeaderId.MessageId];

        return string.IsNullOrWhiteSpace(messageId) ? $"<missing-{Guid.NewGuid():N}>" : messageId;
    }

    private static string? ExtractBody(MimeMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            return message.TextBody;
        }

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            return WebUtility.HtmlDecode(Regex.Replace(message.HtmlBody, "<[^>]+>", " ")).Trim();
        }

        return null;
    }
}
