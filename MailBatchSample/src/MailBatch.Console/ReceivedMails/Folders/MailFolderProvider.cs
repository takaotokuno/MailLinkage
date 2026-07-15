using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Imap;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Folders;

internal sealed class MailFolderProvider(
    ImapOptions imapOptions,
    ProcessingOptions processingOptions,
    IImapConnection imapConnection,
    ILogger<MailFolderProvider> logger) : IMailFolderProvider
{
    public IMailFolder? ReceiveFolder
    {
        get; private set;
    }

    public IMailFolder? ProcessedFolder
    {
        get; private set;
    }

    public async Task PrepareFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        ReceiveFolder = await GetOrCreateReceiveFolderAsync(cancellationToken);

        _ = await ReceiveFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        ProcessedFolder = await GetOrCreateProcessedSubfolderAsync(ReceiveFolder, cancellationToken);

        logger.LogInformation(
            "Prepared IMAP folders. Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}",
            imapOptions.Mailbox,
            processingOptions.ProcessedMailbox);
    }

    public IMailFolder GetOpenedReceiveFolder()
    {
        return ReceiveFolder?.IsOpen != true
            ? throw new InvalidOperationException(
                "Receive mailbox is not open. Call PrepareFoldersAsync before operating mail folders.")
            : ReceiveFolder;
    }

    public async Task<IMailFolder> GetOrCreateProcessedFolderAsync(
        CancellationToken cancellationToken = default)
    {
        if (ProcessedFolder is null)
        {
            IMailFolder receiveFolder = GetOpenedReceiveFolder();

            ProcessedFolder = await GetOrCreateProcessedSubfolderAsync(
                receiveFolder,
                cancellationToken);
        }

        return ProcessedFolder;
    }

    public void Clear()
    {
        ReceiveFolder = null;
        ProcessedFolder = null;
    }

    private async Task<IMailFolder> GetOrCreateReceiveFolderAsync(
        CancellationToken cancellationToken)
    {
        string folderName = imapOptions.Mailbox;

        try
        {
            return await imapConnection.Client.GetFolderAsync(
                folderName,
                cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            IMailFolder namespaceRoot = GetPersonalNamespaceRoot();

            return await CreateFolderAsync(namespaceRoot, folderName, cancellationToken);
        }
    }

    private async Task<IMailFolder> GetOrCreateProcessedSubfolderAsync(
        IMailFolder receiveFolder,
        CancellationToken cancellationToken)
    {
        string folderName = processingOptions.ProcessedMailbox;

        try
        {
            return await receiveFolder.GetSubfolderAsync(
                folderName,
                cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            return await CreateFolderAsync(
                receiveFolder,
                folderName,
                cancellationToken);
        }
    }

    private IMailFolder GetPersonalNamespaceRoot()
    {
        if (imapConnection.Client.PersonalNamespaces.Count == 0)
        {
            throw new InvalidOperationException(
                "The IMAP server does not expose a personal namespace.");
        }

        FolderNamespace personalNamespace =
            imapConnection.Client.PersonalNamespaces[0];

        return imapConnection.Client.GetFolder(personalNamespace);
    }

    private static async Task<IMailFolder> CreateFolderAsync(
        IMailFolder parentFolder,
        string folderName,
        CancellationToken cancellationToken)
    {
        IMailFolder? createdFolder = await parentFolder.CreateAsync(
            folderName,
            isMessageFolder: true,
            cancellationToken);

        // MailKitの契約上、作成失敗時は自動でエラーをスローする。
        return createdFolder!;
    }
}
