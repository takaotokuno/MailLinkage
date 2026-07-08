using System.Threading.Channels;
using MailBatch.Console.Mail;
using MailBatch.Console.Models;
using MailBatch.Console.Notifications;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Services;

internal sealed class MailFetchQueueProducer(
    IMailFolder folder,
    ChannelWriter<ReceivedMailRequest> writer,
    SemaphoreSlim imapLock,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
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
                await ProduceSingleAsync(uid, result);
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
    /// 指定されたUIDのメールを1件処理し、処理結果を集計します。
    /// </summary>
    private async Task ProduceSingleAsync(UniqueId uid, ProcessResultAccumulator result)
    {
        try
        {
            ReceivedMailRequest request = await CreateRequestAsync(uid);
            if (!await ValidateRequestAsync(request, result))
            {
                return;
            }

            await QueueRequestAsync(request, result);
        }
        catch (Exception ex)
        {
            result.IncrementFailure();
            logger.LogError(ex, "Failed to fetch, transform, validate, or queue message. Uid={Uid}", uid);
        }
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
            return ReceivedMailMapper.ToRequest(message, summary.FirstOrDefault()?.InternalDate) with { Uid = uid };
        }
        finally
        {
            imapLock.Release();
        }
    }

    /// <summary>
    /// 受信メールリクエストを検証し、エラーがある場合は通知と集計を行います。
    /// </summary>
    private async Task<bool> ValidateRequestAsync(ReceivedMailRequest request, ProcessResultAccumulator result)
    {
        IReadOnlyList<string> validationErrors = request.Validate();
        if (validationErrors.Count == 0)
        {
            return true;
        }

        await NotifyValidationErrorAsync(request, validationErrors);
        result.IncrementFailure();
        logger.LogWarning(
            "Validation failed for received mail request. MessageId={MessageId}, Errors={ValidationErrors}",
            request.MessageId,
            string.Join("; ", validationErrors));

        return false;
    }

    /// <summary>
    /// 検証済みの受信メールリクエストを内部キューへ追加し、成功件数を集計します。
    /// </summary>
    private async Task QueueRequestAsync(ReceivedMailRequest request, ProcessResultAccumulator result)
    {
        await writer.WriteAsync(request);
        result.IncrementSuccess();
        logger.LogInformation(
            "Queued API request. MessageId={MessageId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
            request.MessageId,
            result.Succeeded,
            request.Body?.Length ?? 0);
    }

    /// <summary>
    /// バリデーションエラーの内容をメール送信元へ通知します。
    /// </summary>
    private async Task NotifyValidationErrorAsync(ReceivedMailRequest request, IReadOnlyList<string> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(request.Sender))
        {
            logger.LogWarning("Cannot send validation error notification because sender is empty. MessageId={MessageId}", request.MessageId);
            return;
        }

        await mailNotifier.SendAsync(mailNotificationFactory.CreateValidationErrorNotification(request, validationErrors));
    }
}
