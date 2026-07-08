using System.Net.Http.Json;
using System.Threading.Channels;
using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Services;

internal sealed class ApiQueueConsumer(
    AppOptions options,
    IMailFolder folder,
    HttpClient httpClient,
    ChannelReader<ReceivedMailRequest> reader,
    SemaphoreSlim imapLock,
    ILogger<ApiQueueConsumer> logger)
{
    /// <summary>
    /// 内部キューからAPI送信用データを順次取り出し、APIへPOSTします。
    /// </summary>
    public async Task<ProcessResult> ConsumeAsync()
    {
        ProcessResultAccumulator result = new();
        logger.LogInformation("API consumer started. Endpoint={Endpoint}", options.Api.Endpoint);

        await foreach (ReceivedMailRequest request in reader.ReadAllAsync())
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["MessageId"] = request.MessageId }))
            {
                bool succeeded = await PostAndHandleResultAsync(request);
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
    private async Task<bool> PostAndHandleResultAsync(ReceivedMailRequest request)
    {
        try
        {
            return await PostMessageAsync(request);
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
    private async Task<bool> PostMessageAsync(ReceivedMailRequest request)
    {
        logger.LogInformation(
            "Posting queued API request. MessageId={MessageId}, Subject={Subject}",
            request.MessageId,
            request.Subject);

        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(options.Api.Endpoint, request);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LogApiFailure(request.MessageId, (int)response.StatusCode, responseBody);
            return false;
        }

        await HandleSuccessfulPostAsync(request.Uid, request, (int)response.StatusCode, responseBody);
        return true;
    }

    /// <summary>
    /// API送信成功時のログ出力と処理済みメールボックスへの移動を行います。
    /// </summary>
    private async Task HandleSuccessfulPostAsync(UniqueId uid, ReceivedMailRequest request, int statusCode, string responseBody)
    {
        LogApiSuccess(request.MessageId, statusCode, responseBody);
        await MoveToProcessedMailboxAsync(uid, request.MessageId);
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
    private async Task MoveToProcessedMailboxAsync(UniqueId uid, string messageId)
    {
        await imapLock.WaitAsync();
        try
        {
            IMailFolder destinationFolder = await GetOrCreateProcessedMailboxAsync();
            await folder.MoveToAsync(uid, destinationFolder);
        }
        finally
        {
            imapLock.Release();
        }

        logger.LogInformation(
            "Moved processed message. MessageId={MessageId}, DestinationMailbox={DestinationMailbox}",
            messageId,
            options.Processing.ProcessedMailbox);
    }

    /// <summary>
    /// 処理済みメールボックスを取得し、存在しない場合は作成します。
    /// </summary>
    private async Task<IMailFolder> GetOrCreateProcessedMailboxAsync()
    {
        try
        {
            return await folder.GetSubfolderAsync(options.Processing.ProcessedMailbox);
        }
        catch (FolderNotFoundException)
        {
            return await folder.CreateAsync(options.Processing.ProcessedMailbox, true);
        }
    }
}
