using MailKit;
using MimeKit;

namespace MailBatch.Console.ReceivedMails;

internal sealed record ReceivedMailContent(
    UniqueId Uid,
    MimeMessage Message,
    DateTimeOffset? InternalDate);
