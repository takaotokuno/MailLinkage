using MimeKit;

namespace MailBatch.Console.ReceivedMails;

internal interface IReceivedMailMapper
{
    ReceivedMailRequest ToRequest(MimeMessage message, DateTimeOffset? internalDate);
}
