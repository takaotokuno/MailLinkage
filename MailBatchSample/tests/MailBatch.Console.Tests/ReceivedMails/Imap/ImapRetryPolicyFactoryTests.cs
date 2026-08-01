using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Imap;
using MailKit.Security;
using Polly;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Imap;

public sealed class ImapRetryPolicyFactoryTests
{

    /// <summary>
    /// 一時的なI/O障害を設定回数だけ再試行することを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: リトライ3回・待機0秒のポリシーへ常にIOExceptionを送出する処理を渡す。<br/>
    /// 期待結果: IOExceptionを再送出するまでに初回を含め合計4回実行する。<br/>
    /// 検知したい異常: I/O障害を再試行しない、または設定回数を超えて実行する不具合。
    /// </remarks>
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

        _ = await Assert.ThrowsAsync<IOException>(() =>
        {
            return policy.ExecuteAsync(() =>
                    {
                        attemptCount++;
                        throw new IOException("Temporary IMAP connection failure.");
                    });
        });
        Assert.Equal(4, attemptCount);
    }

    /// <summary>
    /// 認証失敗を再試行対象にしないことを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: リトライ3回のポリシーへAuthenticationExceptionを送出する処理を渡す。<br/>
    /// 期待結果: AuthenticationExceptionを初回で再送出し、実行回数は1回になる。<br/>
    /// 検知したい異常: 恒久的な認証エラーを無駄に再試行する不具合。
    /// </remarks>
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

        _ = await Assert.ThrowsAsync<AuthenticationException>(() =>
        {
            return policy.ExecuteAsync(() =>
                    {
                        attemptCount++;
                        throw new AuthenticationException("Invalid credentials.");
                    });
        });
        Assert.Equal(1, attemptCount);
    }
}
