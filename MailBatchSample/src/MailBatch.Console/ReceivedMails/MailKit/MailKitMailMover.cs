using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Folders;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.MailKit;

/// <summary>
/// 処理結果に応じてメールを処理済みまたはエラー用メールボックスへ移動します。
/// </summary>
internal interface IMailKitMailMover
{
    /// <summary>
    /// 指定されたメールを処理済みメールボックスへ移動します。
    /// </summary>
    Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定されたメールをエラーメールボックスへ移動します。
    /// </summary>
    Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 処理結果に応じたメール移動を実行します。
/// </summary>
internal sealed class MailKitMailMover(
    ProcessingOptions processingOptions,
    IMailFolderProvider mailFolderProvider,
    ILogger<MailKitMailMover> logger) : IMailKitMailMover
{
    /// <summary>
    /// 指定されたメールを処理済みメールボックスへ移動します。
    /// </summary>
    public async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        IMailFolder processedFolder = await mailFolderProvider.GetOrCreateProcessedFolderAsync(cancellationToken);
        await MoveToMailboxAsync(mailId, processedFolder, processingOptions.ProcessedMailbox, cancellationToken);
    }

    /// <summary>
    /// 指定されたメールをエラーメールボックスへ移動します。
    /// </summary>
    public async Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        IMailFolder errorFolder = await mailFolderProvider.GetOrCreateErrorFolderAsync(cancellationToken);
        await MoveToMailboxAsync(mailId, errorFolder, processingOptions.ErrorMailbox, cancellationToken);
    }

    /// <summary>
    /// 指定されたメールを移動先メールボックスへ移動します。
    /// </summary>
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
