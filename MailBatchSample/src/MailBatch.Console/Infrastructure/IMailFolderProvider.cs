using MailKit;

namespace MailBatch.Console.Infrastructure;

internal interface IMailFolderProvider
{
    IMailFolder? ReceiveFolder { get; }

    IMailFolder? ProcessedFolder { get; }

    Task PrepareFoldersAsync(CancellationToken cancellationToken = default);

    IMailFolder GetOpenedReceiveFolder();

    Task<IMailFolder> GetOrCreateProcessedFolderAsync(CancellationToken cancellationToken = default);

    void Clear();
}
