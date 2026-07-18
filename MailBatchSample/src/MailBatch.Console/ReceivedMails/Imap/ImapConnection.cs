using MailBatch.Console.Options;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Imap;

/// <summary>
/// MailKitのIMAPクライアントを生成し、接続・認証・切断を行います。
/// </summary>
internal sealed class ImapConnection(
    ImapOptions imapOptions,
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

    /// <summary>
    /// IMAPサーバーへ接続して認証します。
    /// </summary>
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
            imapOptions.Host,
            imapOptions.Port,
            imapOptions.SecureSocketOption);

        await imapClient.ConnectAsync(
            imapOptions.Host,
            imapOptions.Port,
            imapOptions.GetSecureSocketOptions(),
            cancellationToken);

        await imapClient.AuthenticateAsync(imapOptions.UserName, imapOptions.Password, cancellationToken);
    }

    /// <summary>
    /// IMAPサーバーとの接続を切断します。
    /// </summary>
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

    /// <summary>
    /// 非同期リソースを解放します。
    /// </summary>
    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
