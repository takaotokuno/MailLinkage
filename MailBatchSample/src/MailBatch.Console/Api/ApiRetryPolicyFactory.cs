using MailBatch.Console.Options;
using Polly;
using Polly.Extensions.Http;

namespace MailBatch.Console.Api;

internal static class ApiRetryPolicyFactory
{
    public static IAsyncPolicy<HttpResponseMessage> Create(ApiOptions apiOptions)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                apiOptions.RetryCount,
                retryAttempt =>
                {
                    return TimeSpan.FromSeconds(apiOptions.RetryDelaySeconds * Math.Pow(2, retryAttempt - 1));
                });
    }
}
