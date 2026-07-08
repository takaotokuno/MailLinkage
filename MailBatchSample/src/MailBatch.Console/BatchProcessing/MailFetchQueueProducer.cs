using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Models;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal interface IMailFetchQueueProducer
{
    Task<ProcessResult> ProduceAsync(IReadOnlyList<UniqueId> targetUids, CancellationToken cancellationToken = default);
}

internal sealed class MailFetchQueueProducer(
    IReceivedMailFolderService receivedMailFolderService,
    ChannelWriter<ReceivedMailRequest> writer,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> logger)
{
    /// <summary>
    /// メールを取得し、API送信用データへ加工したうえで内部キューへ追加します。
    /// </summary>
    public async Task<ProcessResult> ProduceAsync(IReadOnlyList<UniqueId> targetUids, CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new();

        foreach (UniqueId uid in targetUids)
        {
            await ProduceSingleAsync(uid, result, cancellationToken);
        }

        writer.Complete();
        logger.LogInformation(
            "Producer completed queue additions. Enqueued={Enqueued}, Failed={Failed}",
            result.Succeeded,
            result.Failed);

        return result.ToResult();
    }

    /// <summary>
    /// 指定されたUIDのメールを1件処理し、処理結果を集計します。
    /// </summary>
    private async Task ProduceSingleAsync(UniqueId uid, ProcessResultAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            ReceivedMailRequest request = await CreateRequestAsync(uid, cancellationToken);

            await ValidateRequestAsync(request, cancellationToken);
            await QueueRequestAsync(request, cancellationToken);

            result.IncrementSuccess();

            logger.LogInformation(
                "Queued API request. MessageId={MessageId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
                request.MessageId,
                result.Succeeded,
                request.Body?.Length ?? 0);
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
                "Failed to fetch, transform, validate, or queue message. Uid={Uid}",
                uid);
        }
    }

    /// <summary>
    /// 指定されたUIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    private async Task<ReceivedMailRequest> CreateRequestAsync(UniqueId uid, CancellationToken cancellationToken)
    {
        return await receivedMailFolderService.CreateRequestAsync(uid, cancellationToken);
    }

    /// <summary>
    /// 受信メールリクエストを検証し、エラーがある場合は通知を行います。
    /// </summary>
    private async Task ValidateRequestAsync(ReceivedMailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            request.Validate();
        }
        catch (ReceivedMailRequestValidationException ex)
        {
            await NotifyValidationErrorAsync(request, ex.Errors, cancellationToken);

            logger.LogWarning(
                "Validation failed for received mail request. MessageId={MessageId}, Errors={ValidationErrors}",
                request.MessageId,
                string.Join("; ", ex.Errors));

            throw;
        }
    }

    /// <summary>
    /// 検証済みの受信メールリクエストを内部キューへ追加します。
    /// </summary>
    private async Task QueueRequestAsync(ReceivedMailRequest request, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// バリデーションエラーの内容をメール送信元へ通知します。
    /// </summary>
    private async Task NotifyValidationErrorAsync(ReceivedMailRequest request, IReadOnlyList<string> validationErrors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Sender))
        {
            logger.LogWarning(
                "Cannot send validation error notification because sender is empty. MessageId={MessageId}",
                request.MessageId);
            return;
        }

        MailNotification notification = mailNotificationFactory.CreateValidationErrorNotification(request, validationErrors);

        await mailNotifier.SendAsync(notification, cancellationToken);
    }
}
