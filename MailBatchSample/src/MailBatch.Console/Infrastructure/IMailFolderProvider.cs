using MailKit;

namespace MailBatch.Console.Infrastructure;

internal interface IMailFolderProvider
{
    Task<IMailFolder> GetOpenedReceiveFolderAsync(CancellationToken cancellationToken = default);

    Task<IMailFolder> GetProcessedFolderAsync(IMailFolder receiveFolder, CancellationToken cancellationToken = default);

    void Reset();
}
