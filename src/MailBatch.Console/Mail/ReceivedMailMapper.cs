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
            MessageId: string.IsNullOrWhiteSpace(message.MessageId) ? $"<missing-{Guid.NewGuid():N}>" : message.MessageId,
            Sender: message.From.Mailboxes.FirstOrDefault()?.Address ?? message.From.ToString(),
            Subject: message.Subject ?? string.Empty,
            Body: ExtractBody(message),
            ReceivedAt: receivedAt);
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
