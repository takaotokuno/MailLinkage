using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Imap;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Folders;

internal sealed class MailFolderProvider(
    AppOptions options,
    IImapConnection imapConnection,
    ILogger<MailFolderProvider> logger) : IMailFolderProvider
{
    public IMailFolder? ReceiveFolder { get; private set; }

    public IMailFolder? ProcessedFolder { get; private set; }

    public async Task PrepareFoldersAsync(CancellationToken cancellationToken = default)
    {
        ReceiveFolder = await GetOrCreateFolderAsync(options.Imap.Mailbox, cancellationToken);
        await ReceiveFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        ProcessedFolder = await GetOrCreateProcessedFolderAsync(cancellationToken);

        logger.LogInformation(
            "Prepared IMAP folders. Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}",
            options.Imap.Mailbox,
            options.Processing.ProcessedMailbox);
    }

    public IMailFolder GetOpenedReceiveFolder()
    {
        if (ReceiveFolder?.IsOpen != true)
        {
            throw new InvalidOperationException("Receive mailbox is not open. Call ConnectAsync before operating mail folders.");
        }

        return ReceiveFolder;
    }

    public async Task<IMailFolder> GetOrCreateProcessedFolderAsync(CancellationToken cancellationToken = default)
    {
        IMailFolder receiveFolder = GetOpenedReceiveFolder();
        ProcessedFolder ??= await GetOrCreateProcessedMailboxAsync(receiveFolder, cancellationToken);
        return ProcessedFolder;
    }

    public void Clear()
    {
        ReceiveFolder = null;
        ProcessedFolder = null;
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(string folderName, CancellationToken cancellationToken)
    {
        try
        {
            return await imapConnection.Client.GetFolderAsync(folderName, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            if (imapConnection.Client.PersonalNamespaces.Count == 0)
            {
                throw;
            }

            IMailFolder root = imapConnection.Client.GetFolder(imapConnection.Client.PersonalNamespaces[0]);
            return await root.CreateAsync(folderName, true, cancellationToken);
        }
    }

    private async Task<IMailFolder> GetOrCreateProcessedMailboxAsync(IMailFolder folder, CancellationToken cancellationToken)
    {
        try
        {
            return await folder.GetSubfolderAsync(options.Processing.ProcessedMailbox, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            return await folder.CreateAsync(options.Processing.ProcessedMailbox, true, cancellationToken);
        }
    }
}
