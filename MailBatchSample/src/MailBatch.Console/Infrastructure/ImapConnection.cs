using MailBatch.Console.Options;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Infrastructure;

internal sealed class ImapConnection(
    AppOptions options,
    ILogger<ImapConnection> logger) : IImapConnection
{
    private readonly SemaphoreSlim syncRoot = new(1, 1);
    private ImapClient? client;

    public SemaphoreSlim SyncRoot => syncRoot;

    public ImapClient? Client => client;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (client?.IsConnected == true && client.IsAuthenticated)
        {
            return;
        }

        client?.Dispose();
        client = new ImapClient();

        logger.LogInformation(
            "Connecting to IMAP server. Host={Host}, Port={Port}, SecureSocketOption={SecureSocketOption}",
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.SecureSocketOption);

        await client.ConnectAsync(
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.GetSecureSocketOptions(),
            cancellationToken);
        await client.AuthenticateAsync(options.Imap.UserName, options.Imap.Password, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (client?.IsConnected == true)
        {
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Disconnected from IMAP server.");
        }

        client?.Dispose();
        client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        syncRoot.Dispose();
    }
}
