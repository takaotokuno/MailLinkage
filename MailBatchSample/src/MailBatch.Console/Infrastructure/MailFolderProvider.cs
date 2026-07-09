using MailBatch.Console.Options;
using MailKit;

namespace MailBatch.Console.Infrastructure;

internal sealed class MailFolderProvider(
    IImapConnection connection,
    AppOptions options) : IMailFolderProvider
{
    private IMailFolder? receiveFolder;
    private IMailFolder? processedFolder;

    public async Task<IMailFolder> GetOpenedReceiveFolderAsync(CancellationToken cancellationToken = default)
    {
        if (receiveFolder?.IsOpen == true)
        {
            return receiveFolder;
        }

        receiveFolder = await GetOrCreateFolderAsync(options.Imap.Mailbox, cancellationToken);
        await receiveFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        return receiveFolder;
    }

    public async Task<IMailFolder> GetProcessedFolderAsync(IMailFolder receiveFolder, CancellationToken cancellationToken = default)
    {
        processedFolder ??= await GetOrCreateProcessedMailboxAsync(receiveFolder, cancellationToken);
        return processedFolder;
    }

    public void Reset()
    {
        receiveFolder = null;
        processedFolder = null;
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(string folderName, CancellationToken cancellationToken)
    {
        var client = connection.Client ?? throw new InvalidOperationException("IMAP client is not connected.");

        try
        {
            return await client.GetFolderAsync(folderName, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            if (client.PersonalNamespaces.Count == 0)
            {
                throw;
            }

            IMailFolder root = client.GetFolder(client.PersonalNamespaces[0]);
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
