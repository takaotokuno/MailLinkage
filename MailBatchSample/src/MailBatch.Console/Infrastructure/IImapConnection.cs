using MailKit.Net.Imap;

namespace MailBatch.Console.Infrastructure;

internal interface IImapConnection : IAsyncDisposable
{
    SemaphoreSlim SyncRoot { get; }

    ImapClient? Client { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
