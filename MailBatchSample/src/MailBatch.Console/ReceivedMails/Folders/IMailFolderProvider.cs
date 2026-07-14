using MailKit;

namespace MailBatch.Console.ReceivedMails.Folders;

internal interface IMailFolderProvider
{
    IMailFolder? ReceiveFolder { get; }

    IMailFolder? ProcessedFolder { get; }

    Task PrepareFoldersAsync(CancellationToken cancellationToken = default);

    IMailFolder GetOpenedReceiveFolder();

    Task<IMailFolder> GetOrCreateProcessedFolderAsync(CancellationToken cancellationToken = default);

    void Clear();
}
