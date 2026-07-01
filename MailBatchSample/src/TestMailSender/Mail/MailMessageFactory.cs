using MimeKit;
using MimeKit.Utils;
using TestMailSender.Options;

namespace TestMailSender.Mail;

internal static class MailMessageFactory
{
    /// <summary>
    /// 指定されたメール送信オプションに基づいて、検証用メールメッセージを作成します。
    /// </summary>
    public static MimeMessage Create(AppOptions options)
    {
        var mode = options.Mail.Mode.Trim().ToLowerInvariant();
        var subject = mode switch
        {
            "target" => options.Mail.TargetSubject,
            "nontarget" or "non-target" => options.Mail.NonTargetSubject,
            "duplicate" => options.Mail.TargetSubject,
            _ => options.Mail.Subject ?? throw new InvalidOperationException("Mail:Subject is required when Mail:Mode is custom.")
        };

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.Mail.From));
        message.To.Add(MailboxAddress.Parse(options.Mail.To));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = options.Mail.Body };
        message.Date = DateTimeOffset.UtcNow;
        message.MessageId = mode == "duplicate"
            ? options.Mail.DuplicateMessageId
            : MimeUtils.GenerateMessageId("example.local");

        return message;
    }
}
