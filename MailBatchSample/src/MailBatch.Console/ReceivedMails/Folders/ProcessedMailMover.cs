using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.MailKit;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Folders;

/// <summary>
/// 処理結果に応じてメールを処理済みまたはエラー用メールボックスへ移動します。
/// </summary>
internal interface IProcessedMailMover
{
    Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 処理結果に応じたメール移動を実行します。
/// </summary>
internal sealed class ProcessedMailMover(
    ProcessingOptions processingOptions,
    IMailFolderProvider mailFolderProvider,
    ILogger<ProcessedMailMover> logger) : IProcessedMailMover
{
    public async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        IMailFolder processedFolder = await mailFolderProvider.GetOrCreateProcessedFolderAsync(cancellationToken);
        await MoveToMailboxAsync(mailId, processedFolder, processingOptions.ProcessedMailbox, cancellationToken);
    }

    public async Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        IMailFolder errorFolder = await mailFolderProvider.GetOrCreateErrorFolderAsync(cancellationToken);
        await MoveToMailboxAsync(mailId, errorFolder, processingOptions.ErrorMailbox, cancellationToken);
    }

    private async Task MoveToMailboxAsync(
        ReceivedMailId mailId,
        IMailFolder destinationFolder,
        string destinationMailbox,
        CancellationToken cancellationToken)
    {
        UniqueId sourceUid = MailKitReceivedMailIdMapper.ToUniqueId(mailId);
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        UniqueId? destinationUid = await folder.MoveToAsync(sourceUid, destinationFolder, cancellationToken);

        logger.LogInformation(
            "Moved message. SourceMailbox={SourceMailbox}, DestinationMailbox={DestinationMailbox}, SourceMailId={SourceMailId}, SourceUidValidity={SourceUidValidity}, DestinationMailId={DestinationMailId}",
            folder.FullName,
            destinationMailbox,
            sourceUid.Id,
            sourceUid.Validity,
            destinationUid?.Id);
    }
}
