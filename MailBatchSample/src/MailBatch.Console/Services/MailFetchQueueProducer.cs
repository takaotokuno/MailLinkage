using System.Threading.Channels;
using MailBatch.Console.Mail;
using MailBatch.Console.Models;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Services;

internal sealed class MailFetchQueueProducer(
    IMailFolder folder,
    ChannelWriter<ApiQueueItem> writer,
    SemaphoreSlim imapLock,
    ILogger<MailFetchQueueProducer> logger)
{
    /// <summary>
    /// メールを取得し、API送信用データへ加工したうえで内部キューへ追加します。
    /// </summary>
    public async Task<ProcessResult> ProduceAsync(IReadOnlyList<UniqueId> targetUids)
    {
        ProcessResultAccumulator result = new();

        try
        {
            foreach (UniqueId uid in targetUids)
            {
                try
                {
                    ReceivedMailRequest request = await CreateRequestAsync(uid);
                    await writer.WriteAsync(new ApiQueueItem(uid, request));
                    result.IncrementSuccess();
                    logger.LogInformation(
                        "Queued API request. MessageId={MessageId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
                        request.MessageId,
                        result.Succeeded,
                        request.Body?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    result.IncrementFailure();
                    logger.LogError(ex, "Failed to fetch, transform, or queue message. Uid={Uid}", uid);
                }
            }
        }
        finally
        {
            writer.Complete();
            logger.LogInformation("Producer completed queue additions. Enqueued={Enqueued}, Failed={Failed}", result.Succeeded, result.Failed);
        }

        return result.ToResult();
    }

    /// <summary>
    /// 指定されたUIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    private async Task<ReceivedMailRequest> CreateRequestAsync(UniqueId uid)
    {
        await imapLock.WaitAsync();
        try
        {
            MimeKit.MimeMessage message = await folder.GetMessageAsync(uid);
            IList<IMessageSummary> summary = await folder.FetchAsync(new[] { uid }, MessageSummaryItems.InternalDate);
            return ReceivedMailMapper.ToRequest(message, summary.FirstOrDefault()?.InternalDate);
        }
        finally
        {
            imapLock.Release();
        }
    }
}
