using System.Net;
using System.Text.RegularExpressions;
using MimeKit;

namespace MailBatch.Console.ReceivedMails.Fetching;

internal interface IReceivedMailMapper
{
    ReceivedMailRequest ToRequest(MimeMessage message, DateTimeOffset? internalDate);
}

internal sealed class ReceivedMailMapper : IReceivedMailMapper
{
    /// <summary>
    /// MIMEメッセージと内部受信日時から、API送信用の受信メールリクエストを生成します。
    /// </summary>
    public ReceivedMailRequest ToRequest(MimeMessage message, DateTimeOffset? internalDate)
    {
        DateTimeOffset receivedAt = internalDate?.ToUniversalTime()
            ?? (message.Date != DateTimeOffset.MinValue ? message.Date.ToUniversalTime() : DateTimeOffset.UtcNow);

        return new ReceivedMailRequest(
            MessageId: GetMessageId(message),
            Sender: message.From.Mailboxes.FirstOrDefault()?.Address ?? message.From.ToString(),
            Subject: message.Subject ?? string.Empty,
            Body: ExtractBody(message),
            ReceivedAt: receivedAt);
    }

    /// <summary>
    /// メッセージIDヘッダーを取得し、存在しない場合は代替IDを生成します。
    /// </summary>
    private static string GetMessageId(MimeMessage message)
    {
        string? messageId = message.Headers[HeaderId.MessageId];

        return string.IsNullOrWhiteSpace(messageId) ? $"<missing-{Guid.NewGuid():N}>" : messageId;
    }

    /// <summary>
    /// メール本文をテキスト本文またはHTML本文から抽出します。
    /// </summary>
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
