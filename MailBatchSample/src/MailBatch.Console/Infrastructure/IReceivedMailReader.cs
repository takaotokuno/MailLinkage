using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Infrastructure;

internal interface IReceivedMailReader
{
    Task<ReceivedMailContent> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}
