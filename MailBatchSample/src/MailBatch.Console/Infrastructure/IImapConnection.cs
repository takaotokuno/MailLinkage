using MailKit.Net.Imap;

namespace MailBatch.Console.Infrastructure;

internal interface IImapConnection : IAsyncDisposable
{
    ImapClient Client { get; }

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
