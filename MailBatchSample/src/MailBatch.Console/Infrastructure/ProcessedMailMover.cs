using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Infrastructure;

internal sealed class ProcessedMailMover(
    AppOptions options,
    IMailFolderProvider mailFolderProvider,
    ILogger<ProcessedMailMover> logger) : IProcessedMailMover
{
    public async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, string messageId, CancellationToken cancellationToken = default)
    {
        UniqueId uid = MailKitReceivedMailIdMapper.ToUniqueId(mailId);
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        IMailFolder processedFolder = await mailFolderProvider.GetOrCreateProcessedFolderAsync(cancellationToken);
        await folder.MoveToAsync(uid, processedFolder, cancellationToken);

        logger.LogInformation(
            "Moved processed message. MessageId={MessageId}, DestinationMailbox={DestinationMailbox}",
            messageId,
            options.Processing.ProcessedMailbox);
    }
}
