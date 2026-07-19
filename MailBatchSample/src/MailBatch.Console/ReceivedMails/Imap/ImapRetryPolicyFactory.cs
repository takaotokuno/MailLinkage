using System.Net.Sockets;
using MailBatch.Console.Options;
using Polly;

namespace MailBatch.Console.ReceivedMails.Imap;

/// <summary>
/// IMAPサーバー接続時の一時的な失敗に対するリトライ方針を生成します。
/// </summary>
internal static class ImapRetryPolicyFactory
{
    private const int EXPONENTIAL_BACKOFF_BASE = 2;
    private const int RETRY_ATTEMPT_OFFSET = 1;

    /// <summary>
    /// ソケット、I/O、タイムアウトの一時エラーを指数バックオフで再試行するポリシーを作成します。
    /// </summary>
    public static IAsyncPolicy Create(
        ImapOptions imapOptions,
        Action<Exception, TimeSpan, int>? onRetry = null)
    {
        return Policy
            .Handle<SocketException>()
            .Or<IOException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                imapOptions.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(
                    imapOptions.RetryDelaySeconds * Math.Pow(EXPONENTIAL_BACKOFF_BASE, retryAttempt - RETRY_ATTEMPT_OFFSET)),
                (exception, delay, retryAttempt, _) => onRetry?.Invoke(exception, delay, retryAttempt));
    }
}
