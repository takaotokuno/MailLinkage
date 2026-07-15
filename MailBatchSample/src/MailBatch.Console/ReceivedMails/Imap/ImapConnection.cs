using MailBatch.Console.Options;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Imap;

internal sealed class ImapConnection(
    AppOptions options,
    ILogger<ImapConnection> logger) : IImapConnection
{
    private ImapClient? imapClient;

    public ImapClient Client
    {
        get
        {
            return imapClient
        ?? throw new InvalidOperationException("IMAP client is not connected. Call ConnectAsync before using the client.");
        }
    }

    public bool IsConnected
    {
        get
        {
            return imapClient?.IsConnected == true;
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        imapClient?.Dispose();
        imapClient = new ImapClient();

        logger.LogInformation(
            "Connecting to IMAP server. Host={Host}, Port={Port}, SecureSocketOption={SecureSocketOption}",
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.SecureSocketOption);

        await imapClient.ConnectAsync(
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.GetSecureSocketOptions(),
            cancellationToken);

        await imapClient.AuthenticateAsync(options.Imap.UserName, options.Imap.Password, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (imapClient?.IsConnected == true)
        {
            await imapClient.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Disconnected from IMAP server.");
        }

        imapClient?.Dispose();
        imapClient = null;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
