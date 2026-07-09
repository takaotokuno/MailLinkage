using MailBatch.Console.ReceivedMails;
using MailKit;

namespace MailBatch.Console.Infrastructure;

internal interface IReceivedMailReader
{
    Task<ReceivedMailContent> ReadAsync(IMailFolder receiveFolder, UniqueId uid, CancellationToken cancellationToken = default);
}
