using MailKit;

namespace MailBatch.Console.Infrastructure;

internal interface IProcessedMailMover
{
    Task MoveAsync(IMailFolder receiveFolder, IMailFolder processedFolder, UniqueId uid, CancellationToken cancellationToken = default);
}
