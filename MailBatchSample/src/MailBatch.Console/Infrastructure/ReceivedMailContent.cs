using MimeKit;

namespace MailBatch.Console.Infrastructure;

internal sealed record ReceivedMailContent(MimeMessage Message, DateTimeOffset? InternalDate);
