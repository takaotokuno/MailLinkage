using MailBatch.Console.Options;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;
using Polly;

namespace MailBatch.Console.ReceivedMails.Imap;

/// <summary>
/// MailKitのIMAPクライアントを生成し、接続・認証・切断を行います。
/// </summary>
internal sealed class ImapConnection(
    ImapOptions imapOptions,
    ILogger<ImapConnection> logger) : IImapConnection
{
    private ImapClient? _imapClient;

    public ImapClient Client
    {
        get
        {
            return _imapClient
        ?? throw new InvalidOperationException("IMAP client is not connected. Call ConnectAsync before using the client.");
        }
    }

    public bool IsConnected
    {
        get
        {
            return _imapClient?.IsConnected == true;
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

        IAsyncPolicy retryPolicy = ImapRetryPolicyFactory.Create(
            imapOptions,
            (exception, delay, retryAttempt) =>
            {
                logger.LogWarning(
                    exception,
                    "IMAP connection failed. Retrying after {RetryDelay}. RetryAttempt={RetryAttempt}, RetryCount={RetryCount}",
                    delay,
                    retryAttempt,
                    imapOptions.RetryCount);
            });

        await retryPolicy.ExecuteAsync(async token =>
        {
            _imapClient?.Dispose();
            _imapClient = new ImapClient();

            logger.LogInformation(
                "Connecting to IMAP server. Host={Host}, Port={Port}, SocketOptions={SocketOptions}",
                imapOptions.Host,
                imapOptions.Port,
                imapOptions.SocketOptions);

            await _imapClient.ConnectAsync(
                imapOptions.Host,
                imapOptions.Port,
                imapOptions.SocketOptions,
                token);

            await _imapClient.AuthenticateAsync(imapOptions.UserName, imapOptions.Password, token);
        }, cancellationToken);
    }

    /// <summary>
    /// IMAPサーバーとの接続を切断します。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_imapClient?.IsConnected == true)
        {
            await _imapClient.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Disconnected from IMAP server.");
        }

        _imapClient?.Dispose();
        _imapClient = null;
    }

    /// <summary>
    /// 非同期リソースを解放します。
    /// </summary>
    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
