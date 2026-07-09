using MailKit;

namespace MailBatch.Console.Infrastructure;

internal sealed class ProcessedMailMover : IProcessedMailMover
{
    public Task MoveAsync(IMailFolder receiveFolder, IMailFolder processedFolder, UniqueId uid, CancellationToken cancellationToken = default)
    {
        return receiveFolder.MoveToAsync(uid, processedFolder, cancellationToken);
    }
}
