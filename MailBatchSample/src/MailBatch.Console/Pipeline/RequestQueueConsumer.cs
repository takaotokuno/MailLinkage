using System.Diagnostics;
using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

/// <summary>
/// キューからAPI連携リクエストを取り出して処理します。
/// </summary>
internal interface IRequestQueueConsumer
{
    /// <summary>
    /// API送信用キューのリクエストを消費し、処理結果を返します。
    /// </summary>
    Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// API連携リクエストを送信し、結果に応じてメールを移動します。
/// </summary>
internal sealed class RequestQueueConsumer(
    ApiOptions apiOptions,
    IReceivedMailMover receivedMailMover,
    IApiClient receivedMailApiClient,
    ChannelReader<MailLinkageRequest> reader,
    IProcessedMailStore processedMailStore,
    IMailMoveFailureStore moveFailureStore,
    IApiExecutionResultStore apiExecutionResultStore,
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
            await ConsumeSingleAsync(request, result, cancellationToken);
        }

        LogCompletion(result);
        return result.ToResult();
    }

    /// <summary>
    /// キューから取得したリクエスト1件をAPIへ送信し、結果を集計します。
    /// </summary>
    private async Task ConsumeSingleAsync(
        MailLinkageRequest request,
        ProcessResultAccumulator result,
        CancellationToken cancellationToken)
    {
        using (logger.BeginScope(new Dictionary<string, object> { ["MailId"] = request.MailId }))
        {
            (bool succeeded, string executionId) = await PostAndHandleResultAsync(request, cancellationToken);
            await HandlePostOutcomeAsync(request.MailId, executionId, succeeded, result, cancellationToken);
        }
    }

    /// <summary>
    /// API送信結果に応じてメールを移動し、処理結果を更新します。
    /// </summary>
    private async Task HandlePostOutcomeAsync(
        ReceivedMailId mailId,
        string executionId,
        bool succeeded,
        ProcessResultAccumulator result,
        CancellationToken cancellationToken)
    {
        if (!succeeded)
        {
            ReceivedMailId? movedMailId = await TryMoveToErrorMailboxAsync(mailId, cancellationToken);
            await RecordMovedMailIdAsync(executionId, movedMailId, cancellationToken);
            result.IncrementApiFailure();
            return;
        }

        await processedMailStore.RecordAsync(mailId, cancellationToken);
        (bool moved, ReceivedMailId? movedMailId) = await TryMoveToProcessedMailboxAsync(mailId, cancellationToken);
        await RecordMovedMailIdAsync(executionId, movedMailId, cancellationToken);
        if (moved)
        {
            result.IncrementSuccess();
            return;
        }

        result.IncrementApiFailure();
    }

    /// <summary>
    /// Consumer終了時にAPI送信件数をログへ出力します。
    /// </summary>
    private void LogCompletion(ProcessResultAccumulator result)
    {
        logger.LogInformation(
            "Consumer confirmed no remaining queued data. ApiSucceeded={Succeeded}, ApiFailed={ApiFailed}",
            result.Succeeded,
            result.ApiFailed);
    }

    /// <summary>
    /// メール送信処理を実行し、予期しない例外をログに記録して失敗として扱います。
    /// </summary>
    private async Task<(bool Succeeded, string ExecutionId)> PostAndHandleResultAsync(MailLinkageRequest request, CancellationToken cancellationToken)
    {
        string executionId = Guid.NewGuid().ToString("N");
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        ApiPostResult result;
        try
        {
            ApiRequest apiRequest = new(request.Key, request.Message);
            result = await PostMessageAsync(apiRequest, executionId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
            await apiExecutionResultStore.RecordAsync(new ApiExecutionResult(
                executionId,
                request.MailId,
                apiOptions.Endpoint,
                "Exception",
                null,
                null,
                null,
                ex.GetType().FullName,
                startedAtUtc,
                completedAtUtc,
                stopwatch.ElapsedMilliseconds), cancellationToken);
            logger.LogError(
                ex,
                "Unexpected error while processing queued API request. ExecutionId={ExecutionId}, MailId={MailId}, DurationMs={DurationMs}",
                executionId,
                request.MailId,
                stopwatch.ElapsedMilliseconds);

            return (false, executionId);
        }

        DateTimeOffset resultCompletedAtUtc = DateTimeOffset.UtcNow;
        await apiExecutionResultStore.RecordAsync(new ApiExecutionResult(
            executionId,
            request.MailId,
            apiOptions.Endpoint,
            result.IsSuccess ? "Succeeded" : "Failed",
            result.StatusCode,
            result.IsSuccess ? ApiResponseSummary.ExtractSavedId(result.ResponseBody) : null,
            result.IsSuccess ? null : ApiResponseSummary.Summarize(result.ResponseBody),
            null,
            startedAtUtc,
            resultCompletedAtUtc,
            stopwatch.ElapsedMilliseconds), cancellationToken);
        logger.LogDebug(
            "API execution result recorded. ExecutionId={ExecutionId}, Outcome={Outcome}, DurationMs={DurationMs}",
            executionId,
            result.IsSuccess ? "Succeeded" : "Failed",
            stopwatch.ElapsedMilliseconds);
        return (result.IsSuccess, executionId);
    }

    /// <summary>
    /// 受信メールリクエストをAPIへ送信します。
    /// </summary>
    private async Task<ApiPostResult> PostMessageAsync(ApiRequest request, string executionId, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Posting queued API request. ExecutionId={ExecutionId}, Endpoint={Endpoint}, MessageLength={MessageLength}",
            executionId,
            apiOptions.Endpoint,
            request.Message.Length);

        ApiPostResult result = await receivedMailApiClient.PostReceivedMailAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            LogApiSuccess(executionId, result.StatusCode, result.ResponseBody);
        }
        else
        {
            LogApiFailure(executionId, result.StatusCode, result.ResponseBody);
        }

        return result;
    }

    /// <summary>
    /// API送信成功時のステータスコードと保存済みIDをログに出力します。
    /// </summary>
    private void LogApiSuccess(string executionId, int statusCode, string responseBody)
    {
        logger.LogInformation(
            "API post succeeded. ExecutionId={ExecutionId}, StatusCode={StatusCode}, SavedId={SavedId}",
            executionId,
            statusCode,
            ApiResponseSummary.ExtractSavedId(responseBody));
    }

    /// <summary>
    /// API送信失敗時のステータスコードとレスポンス概要をログに出力します。
    /// </summary>
    private void LogApiFailure(string executionId, int statusCode, string responseBody)
    {
        logger.LogWarning(
            "API post failed. ExecutionId={ExecutionId}, StatusCode={StatusCode}, Response={ResponseSummary}",
            executionId,
            statusCode,
            ApiResponseSummary.Summarize(responseBody));
    }

    /// <summary>
    /// API送信成功後、処理済みメールボックスへ移動します。移動失敗時は再送防止用に記録します。
    /// </summary>
    private async Task<(bool Moved, ReceivedMailId? MovedMailId)> TryMoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken)
    {
        try
        {
            ReceivedMailId? movedMailId = await MoveToProcessedMailboxAsync(mailId, cancellationToken);
            await moveFailureStore.RemoveAsync(mailId, cancellationToken);
            return (true, movedMailId);
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

            return (false, null);
        }
    }

    /// <summary>
    /// API送信失敗後、エラーメールボックスへ移動します。移動失敗時は再処理抑止用に記録します。
    /// </summary>
    private async Task<ReceivedMailId?> TryMoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken)
    {
        try
        {
            return await MoveToErrorMailboxAsync(mailId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await moveFailureStore.AddErrorMoveFailureAsync(mailId, cancellationToken);
            logger.LogError(
                ex,
                "API post failed and moving mail to error mailbox failed. MailId={MailId}",
                mailId);
            return null;
        }
    }

    private async Task RecordMovedMailIdAsync(string executionId, ReceivedMailId? movedMailId, CancellationToken cancellationToken)
    {
        if (movedMailId is not null)
        {
            await apiExecutionResultStore.RecordMovedMailIdAsync(executionId, movedMailId.Value, cancellationToken);
        }
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    private async Task<ReceivedMailId?> MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken) => await receivedMailMover.MoveToProcessedMailboxAsync(mailId, cancellationToken);

    /// <summary>
    /// API連携に失敗したメールを設定されたエラーメールボックスへ移動します。
    /// </summary>
    private async Task<ReceivedMailId?> MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken) => await receivedMailMover.MoveToErrorMailboxAsync(mailId, cancellationToken);
}
