using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.State;
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
    IProcessedMailMoveFailureStore moveFailureStore,
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
                    if (await TryMoveToProcessedMailboxAsync(request.MailId, cancellationToken))
                    {
                        result.IncrementSuccess();
                    }
                    else
                    {
                        result.IncrementApiFailure();
                    }
                }
                else
                {
                    await MoveToErrorMailboxAsync(request.MailId, cancellationToken);
                    result.IncrementApiFailure();
                }
            }
        }

        logger.LogInformation(
            "Consumer confirmed no remaining queued data. ApiSucceeded={Succeeded}, ApiFailed={ApiFailed}",
            result.Succeeded,
            result.ApiFailed);

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
            return await PostMessageAsync(apiRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error while processing queued API request. MailId={MailId}",
                request.MailId);

            return false;
        }
    }

    /// <summary>
    /// 受信メールリクエストをAPIへ送信します。
    /// </summary>
    private async Task<bool> PostMessageAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Posting queued API request. Message={Message}",
            request.Message);

        ApiPostResult result = await receivedMailApiClient.PostReceivedMailAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            LogApiSuccess(result.StatusCode, result.ResponseBody);
            return true;
        }
        else
        {
            LogApiFailure(result.StatusCode, result.ResponseBody);
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
    /// API送信成功後、処理済みメールボックスへ移動します。移動失敗時は再送防止用に記録します。
    /// </summary>
    private async Task<bool> TryMoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken)
    {
        try
        {
            await MoveToProcessedMailboxAsync(mailId, cancellationToken);
            await moveFailureStore.RemoveAsync(mailId, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await moveFailureStore.AddAsync(mailId, cancellationToken);
            logger.LogError(
                ex,
                "API post succeeded but moving mail to processed mailbox failed. MailId={MailId}",
                mailId);

            return false;
        }
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    private async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken) => await receivedMailSession.MoveToProcessedMailboxAsync(mailId, cancellationToken);

    /// <summary>
    /// API連携に失敗したメールを設定されたエラーメールボックスへ移動します。
    /// </summary>
    private async Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken) => await receivedMailSession.MoveToErrorMailboxAsync(mailId, cancellationToken);
}
