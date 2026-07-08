using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;
using MailBatch.Console.Models;
using MailBatch.Console.Infrastructure;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal interface IApiQueueConsumer
{
    Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default);
}

internal sealed class ApiQueueConsumer(
    AppOptions options,
    IReceivedMailFolderService receivedMailFolderService,
    IApiClient receivedMailApiClient,
    ChannelReader<ReceivedMailRequest> reader,
    ILogger<ApiQueueConsumer> logger)
{
    /// <summary>
    /// 内部キューからAPI送信用データを順次取り出し、APIへPOSTします。
    /// </summary>
    public async Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new();
        logger.LogInformation("API consumer started. Endpoint={Endpoint}", options.Api.Endpoint);

        await foreach (ReceivedMailRequest request in reader.ReadAllAsync(cancellationToken))
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = request.MessageId }))
            {
                bool succeeded = await PostAndHandleResultAsync(request, cancellationToken);
                if (succeeded)
                {
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
    private async Task<bool> PostAndHandleResultAsync(ReceivedMailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await PostMessageAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while processing queued API request. MessageId={MessageId}", request.MessageId);
            return false;
        }
    }

    /// <summary>
    /// 受信メールリクエストをAPIへ送信し、レスポンスに応じた後続処理を行います。
    /// </summary>
    private async Task<bool> PostMessageAsync(ReceivedMailRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Posting queued API request. MessageId={MessageId}, Subject={Subject}",
            request.MessageId,
            request.Subject);

        using HttpResponseMessage response = await receivedMailApiClient.PostReceivedMailAsync(request, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            LogApiFailure(request.MessageId, (int)response.StatusCode, responseBody);
            return false;
        }

        await HandleSuccessfulPostAsync(request.Uid, request, (int)response.StatusCode, responseBody, cancellationToken);
        return true;
    }

    /// <summary>
    /// API送信成功時のログ出力と処理済みメールボックスへの移動を行います。
    /// </summary>
    private async Task HandleSuccessfulPostAsync(UniqueId uid, ReceivedMailRequest request, int statusCode, string responseBody, CancellationToken cancellationToken)
    {
        LogApiSuccess(request.MessageId, statusCode, responseBody);
        await MoveToProcessedMailboxAsync(uid, request.MessageId, cancellationToken);
    }

    /// <summary>
    /// API送信成功時のステータスコードと保存済みIDをログに出力します。
    /// </summary>
    private void LogApiSuccess(string messageId, int statusCode, string responseBody)
    {
        logger.LogInformation(
            "API post succeeded. MessageId={MessageId}, StatusCode={StatusCode}, SavedId={SavedId}",
            messageId,
            statusCode,
            ApiResponseSummary.ExtractSavedId(responseBody));
    }

    /// <summary>
    /// API送信失敗時のステータスコードとレスポンス概要をログに出力します。
    /// </summary>
    private void LogApiFailure(string messageId, int statusCode, string responseBody)
    {
        logger.LogWarning(
            "API post failed. MessageId={MessageId}, StatusCode={StatusCode}, Response={ResponseSummary}",
            messageId,
            statusCode,
            ApiResponseSummary.Summarize(responseBody));
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    private async Task MoveToProcessedMailboxAsync(UniqueId uid, string messageId, CancellationToken cancellationToken)
    {
        await receivedMailFolderService.MoveToProcessedMailboxAsync(uid, messageId, cancellationToken);
    }
}
