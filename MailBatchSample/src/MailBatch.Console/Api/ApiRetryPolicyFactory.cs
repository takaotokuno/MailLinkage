using MailBatch.Console.Options;
using Polly;
using Polly.Extensions.Http;

namespace MailBatch.Console.Api;

/// <summary>
/// API送信時の一時的な失敗に対するリトライ方針を生成します。
/// </summary>
internal static class ApiRetryPolicyFactory
{
    private const int EXPONENTIAL_BACKOFF_BASE = 2;
    private const int RETRY_ATTEMPT_OFFSET = 1;

    /// <summary>
    /// インスタンスまたは処理に必要な値を作成します。
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> Create(ApiOptions apiOptions)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            // 一時的なネットワーク断やAPI側の瞬断で即失敗にしないため、指数バックオフで再試行します。
            // 短時間の連続リクエストを避け、障害中のAPIへ負荷を集中させることも防ぎます。
            .WaitAndRetryAsync(
                apiOptions.RetryCount,
                retryAttempt =>
                {
                    return TimeSpan.FromSeconds(apiOptions.RetryDelaySeconds * Math.Pow(EXPONENTIAL_BACKOFF_BASE, retryAttempt - RETRY_ATTEMPT_OFFSET));
                });
    }
}
