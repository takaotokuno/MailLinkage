using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Imap;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Imap;

public sealed class ImapRetryPolicyFactoryTests
{
    /// <summary>
    /// 状態: リトライ回数が3回、待機時間が0秒に設定されている。
    /// 振る舞い: 一時的なI/Oエラーが継続すると、初回を含めて合計4回実行する。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RetriesIoFailureThreeTimes()
    {
        ImapOptions options = new()
        {
            RetryCount = 3,
            RetryDelaySeconds = 0
        };
        int attemptCount = 0;

        IAsyncPolicy policy = ImapRetryPolicyFactory.Create(options);

        _ = await Assert.ThrowsAsync<IOException>(() => policy.ExecuteAsync(() =>
        {
            attemptCount++;
            throw new IOException("Temporary IMAP connection failure.");
        }));
        Assert.Equal(4, attemptCount);
    }

    /// <summary>
    /// 状態: リトライ回数が3回に設定され、認証情報が拒否されている。
    /// 振る舞い: 認証情報のエラーは一時エラーではないため再試行しない。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DoesNotRetryAuthenticationFailure()
    {
        ImapOptions options = new()
        {
            RetryCount = 3,
            RetryDelaySeconds = 0
        };
        int attemptCount = 0;

        IAsyncPolicy policy = ImapRetryPolicyFactory.Create(options);

        _ = await Assert.ThrowsAsync<MailKit.Security.AuthenticationException>(() => policy.ExecuteAsync(() =>
        {
            attemptCount++;
            throw new MailKit.Security.AuthenticationException("Invalid credentials.");
        }));
        Assert.Equal(1, attemptCount);
    }
}
