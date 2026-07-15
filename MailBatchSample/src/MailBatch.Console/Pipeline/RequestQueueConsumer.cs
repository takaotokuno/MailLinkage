using System.Net;
using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

internal interface IRequestQueueConsumer
{
    Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default);
}

internal sealed class RequestQueueConsumer(
    ApiOptions apiOptions,
    IReceivedMailSession receivedMailSession,
    IApiClient receivedMailApiClient,
    ChannelReader<MailLinkageRequest> reader,
    ILogger<MailLinkageRequest> logger) : IRequestQueueConsumer
{
    /// <summary>
    /// 内部キューからAPI送信用データを順次取り出し、APIへPOSTします。
    /// </summary>
    public async Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new();
        logger.LogInformation("API consumer started. Endpoint={Endpoint}", apiOptions.Endpoint);

        await foreach (MailLinkageRequest request in reader.ReadAllAsync(cancellationToken))
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["MailId"] = request.MailId }))
            {
                bool succeeded = await PostAndHandleResultAsync(request, cancellationToken);
                if (succeeded)
                {
                    await MoveToProcessedMailboxAsync(request.MailId, cancellationToken);
                    result.IncrementSuccess();
                }
                else
                {
                    result.IncrementFailure();
                }
            }
        }

        logger.LogInformation(
            "Consumer confirmed no remaining queued data. ApiSucceeded={Succeeded}, ApiFailed={Failed}",
            result.Succeeded,
            result.Failed);

        return result.ToResult();
    }

    /// <summary>
    /// メール送信処理を実行し、予期しない例外をログに記録して失敗として扱います。
    /// </summary>
    private async Task<bool> PostAndHandleResultAsync(MailLinkageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ApiRequest apiRequest = new(request.Message);
            _ = await PostMessageAsync(apiRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error while processing queued API request. MailId={MailId}",
                request.MailId);

            return false;
        }
        return true;
    }

    /// <summary>
    /// 受信メールリクエストをAPIへ送信します。
    /// </summary>
    private async Task<bool> PostMessageAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Posting queued API request. Message={Message}",
            request.Message);

        using HttpResponseMessage response = await receivedMailApiClient.PostReceivedMailAsync(request, cancellationToken);
        HttpStatusCode statusCode = response.StatusCode;
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            LogApiSuccess((int)statusCode, responseBody);
            return true;
        }
        else
        {
            LogApiFailure((int)response.StatusCode, responseBody);
            return false;
        }
    }

    /// <summary>
    /// API送信成功時のステータスコードと保存済みIDをログに出力します。
    /// </summary>
    private void LogApiSuccess(int statusCode, string responseBody)
    {
        logger.LogInformation(
            "API post succeeded. StatusCode={StatusCode}, SavedId={SavedId}",
            statusCode,
            ApiResponseSummary.ExtractSavedId(responseBody));
    }

    /// <summary>
    /// API送信失敗時のステータスコードとレスポンス概要をログに出力します。
    /// </summary>
    private void LogApiFailure(int statusCode, string responseBody)
    {
        logger.LogWarning(
            "API post failed. StatusCode={StatusCode}, Response={ResponseSummary}",
            statusCode,
            ApiResponseSummary.Summarize(responseBody));
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    private async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken) => await receivedMailSession.MoveToProcessedMailboxAsync(mailId, cancellationToken);
}
