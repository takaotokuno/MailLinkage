using MailBatch.Console.Api;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.NotificationMails;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Fetching;

namespace MailBatch.Console.BatchProcessing;

internal interface IMailFetchQueueProducer
{
    Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}

internal sealed class MailFetchQueueProducer(
    IReceivedMailSession receivedMailSession,
    ChannelWriter<ApiRequest> writer,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> logger) : IMailFetchQueueProducer
{
    /// <summary>
    /// メールを取得し、API送信用データへ加工したうえで内部キューへ追加します。
    /// </summary>
    public async Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new();

        foreach (ReceivedMailId mailId in targetMailIds)
        {
            await ProduceSingleAsync(mailId, result, cancellationToken);
        }

        writer.Complete();
        logger.LogInformation(
            "Producer completed queue additions. Enqueued={Enqueued}, Failed={Failed}",
            result.Succeeded,
            result.Failed);

        return result.ToResult();
    }

    /// <summary>
    /// 指定された受信メールIDのメールを1件処理し、処理結果を集計します。
    /// </summary>
    private async Task ProduceSingleAsync(ReceivedMailId mailId, ProcessResultAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            ReceivedMailContent content = await receivedMailSession.CreateRequestAsync(mailId, cancellationToken);

            await ValidateRequestAsync(content, cancellationToken);
            await QueueRequestAsync(content, cancellationToken);

            result.IncrementSuccess();

            logger.LogInformation(
                "Queued API request. MessageId={MessageId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
                content.MailId,
                result.Succeeded,
                content.Body?.Length ?? 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.IncrementFailure();
            logger.LogError(
                ex,
                "Failed to fetch, transform, validate, or queue message. MailId={MailId}",
                mailId);
        }
    }

    /// <summary>
    /// 受信メールリクエストを検証し、エラーがある場合は通知を行います。
    /// </summary>
    private async Task ValidateRequestAsync(ReceivedMailContent content, CancellationToken cancellationToken)
    {
        try
        {
            content.Validate();
        }
        catch (ReceivedMailContentValidationException ex)
        {
            await NotifyValidationErrorAsync(content, ex.Errors, cancellationToken);

            logger.LogWarning(
                "Validation failed for received mail request. MessageId={MessageId}, Errors={ValidationErrors}",
                content.MailId,
                string.Join("; ", ex.Errors));

            throw;
        }
    }

    /// <summary>
    /// 検証済みの受信メールリクエストを内部キューへ追加します。
    /// </summary>
    private async Task QueueRequestAsync(ReceivedMailContent content, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// バリデーションエラーの内容をメール送信元へ通知します。
    /// </summary>
    private async Task NotifyValidationErrorAsync(ReceivedMailContent content, IReadOnlyList<string> validationErrors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content.Sender))
        {
            logger.LogWarning(
                "Cannot send validation error notification because sender is empty. MessageId={MessageId}",
                content.MailId);
            return;
        }

        MailNotification notification = mailNotificationFactory.CreateValidationErrorNotification(content, validationErrors);

        await mailNotifier.SendAsync(notification, cancellationToken);
    }
}
