using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

internal interface IMailFetchQueueProducer
{
    Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}

internal sealed class MailFetchQueueProducer(
    IReceivedMailSession receivedMailSession,
    ChannelWriter<MailLinkageRequest> writer,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    IProcessedMailMoveFailureStore moveFailureStore,
    ILogger<MailFetchQueueProducer> logger) : IMailFetchQueueProducer
{
    /// <summary>
    /// メールを取得し、API送信用データへ加工したうえで内部キューへ追加します。
    /// </summary>
    public async Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new(targetMailIds.Count);
        Exception? fatalException = null;

        try
        {
            foreach (ReceivedMailId mailId in targetMailIds)
            {
                await ProduceSingleAsync(mailId, result, cancellationToken);
            }

            return result.ToResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            fatalException = ex;
            throw;
        }
        finally
        {
            _ = writer.TryComplete(fatalException);
            logger.LogInformation(
                "Producer completed queue additions. Enqueued={Enqueued}, InvalidFormat={InvalidFormat}",
                result.Succeeded,
                result.InvalidFormat);
        }
    }

    /// <summary>
    /// 指定された受信メールIDのメールを1件処理し、処理結果を集計します。
    /// </summary>
    private async Task ProduceSingleAsync(ReceivedMailId mailId, ProcessResultAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            if (await TryRecoverProcessedMoveFailureAsync(mailId, result, cancellationToken))
            {
                return;
            }

            ReceivedMail mail = await receivedMailSession.CreateRequestAsync(mailId, cancellationToken);

            ExtractedMailItem item = await ValidateAndExtractAsync(mail, cancellationToken);

            await QueueRequestAsync(mail, item, cancellationToken);

            result.IncrementSuccess();

            logger.LogInformation(
                "Queued API request. MailId={MailId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
                mail.MailId,
                result.Succeeded,
                mail.Body?.Length ?? 0);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordInvalidFormat(mailId, result, ex);
        }
    }


    /// <summary>
    /// メール取得、変換、キュー投入中の業務処理例外はすべて入力メール形式不正として集計します。
    /// MIME破損、Extract時のキー情報不足、データサイズ上限超過、キュー投入失敗はいずれもここでInvalidFormatへ寄せます。
    /// </summary>
    private void RecordInvalidFormat(ReceivedMailId mailId, ProcessResultAccumulator result, Exception exception)
    {
        result.IncrementInvalidFormat();
        logger.LogError(
            exception,
            "Failed to fetch, transform, validate, or queue message. Counted as InvalidFormat. MailId={MailId}",
            mailId);
    }

    /// <summary>
    /// API送信済みで処理済みメールボックスへの移動だけが未完了のメールを再送せずに移動します。
    /// </summary>
    private async Task<bool> TryRecoverProcessedMoveFailureAsync(ReceivedMailId mailId, ProcessResultAccumulator result, CancellationToken cancellationToken)
    {
        if (!await moveFailureStore.ContainsAsync(mailId, cancellationToken))
        {
            return false;
        }

        try
        {
            await receivedMailSession.MoveToProcessedMailboxAsync(mailId, cancellationToken);
            await moveFailureStore.RemoveAsync(mailId, cancellationToken);
            result.IncrementSuccess();

            logger.LogInformation(
                "Recovered processed mailbox move failure without reposting API request. MailId={MailId}",
                mailId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.IncrementApiFailure();
            logger.LogError(
                ex,
                "Failed to recover processed mailbox move failure. MailId={MailId}",
                mailId);
        }

        return true;
    }

    /// <summary>
    /// 受信メールを検証して連携項目を抽出します。
    /// エラーがある場合は、すべてのエラーをまとめて通知します。
    /// </summary>
    private async Task<ExtractedMailItem> ValidateAndExtractAsync(ReceivedMail mail, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        ExtractedMailItem? mailItem = null;

        try
        {
            mail.Validate();
        }
        catch (ReceivedMailContentValidationException ex)
        {
            errors.AddRange(ex.Errors);
        }

        try
        {
            mailItem = MailItemExtractor.Extract(mail);
        }
        catch (MailExtractionException ex)
        {
            errors.AddRange(ex.Errors);
        }

        if (errors.Count > 0)
        {
            await NotifyValidationErrorAsync(mail, errors, cancellationToken);

            logger.LogWarning(
                "Validation failed for received mail request. MailId={MailId}, Errors={ValidationErrors}",
                mail.MailId,
                string.Join("; ", errors));

            throw new ReceivedMailProcessingException(errors);
        }

        return mailItem
            ?? throw new InvalidOperationException("Mail extraction completed without errors but returned no item.");
    }

    /// <summary>
    /// 検証済みの受信メールリクエストを内部キューへ追加します。
    /// </summary>
    private async Task QueueRequestAsync(
        ReceivedMail mail,
        ExtractedMailItem item,
        CancellationToken cancellationToken)
    {
        MailLinkageRequest request = MailLinkageRequestFactory.Create(mail, item);
        await writer.WriteAsync(request, cancellationToken);
    }

    /// <summary>
    /// バリデーションエラーの内容をメール送信元へ通知します。
    /// </summary>
    private async Task NotifyValidationErrorAsync(ReceivedMail mail, IReadOnlyList<string> validationErrors, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mail.Sender))
        {
            logger.LogWarning(
                "Cannot send validation error notification because sender is empty. MailId={MailId}",
                mail.MailId);
            return;
        }

        MailNotification notification = mailNotificationFactory.CreateValidationErrorNotification(mail, validationErrors);

        try
        {
            await mailNotifier.SendAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send validation error notification. MailId={MailId}, NotificationTo={NotificationTo}",
                mail.MailId,
                notification.To);
        }
    }

    private class ReceivedMailProcessingException(IReadOnlyCollection<string> errors)
        : Exception(string.Join(Environment.NewLine, errors))
    {
        public IReadOnlyList<string> Errors { get; } = errors.ToArray();
    }
}
