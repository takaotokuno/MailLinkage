using System.Threading.Channels;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

/// <summary>
/// メールを読み取りキューへ投入するProducer操作を提供します。
/// </summary>
internal interface IMailFetchQueueProducer
{
    /// <summary>
    /// 指定された受信メールID一覧を取得し、API送信用キューへ投入します。
    /// </summary>
    Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// 処理対象メールを読み取り、API連携リクエストとしてキューへ投入します。
/// </summary>
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
    public async Task<ProcessResult> ProduceAsync(
        IReadOnlyList<ReceivedMailId> targetMailIds,
        CancellationToken cancellationToken = default)
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
    private async Task ProduceSingleAsync(
        ReceivedMailId mailId,
        ProcessResultAccumulator result,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await moveFailureStore.ContainsAsync(mailId, cancellationToken))
            {
                logger.LogWarning(
                    "Skipped message because a mailbox move failure record still exists. MailId={MailId}",
                    mailId);
                return;
            }

            if (await moveFailureStore.IsProcessedAsync(mailId, cancellationToken))
            {
                logger.LogWarning("Skipped message because it already exists in the processed mail ledger. MailId={MailId}", mailId);
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
        catch (ReceivedMailProcessingException ex)
        {
            await RecordInvalidFormatAsync(mailId, result, ex, cancellationToken);
        }
        catch (ReceivedMailFormatException ex)
        {
            await RecordInvalidFormatAsync(mailId, result, ex, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to fetch, transform, or queue message. Counted as system error. MailId={MailId}",
                mailId);
            throw;
        }
    }


    /// <summary>
    /// メールそのものが壊れている、キー情報が不足している、またはデータサイズ上限を超過している場合、入力メール形式不正として集計します。
    /// </summary>
    private async Task RecordInvalidFormatAsync(ReceivedMailId mailId, ProcessResultAccumulator result, Exception exception, CancellationToken cancellationToken)
    {
        result.IncrementInvalidFormat();
        logger.LogError(
            exception,
            "Failed to parse, validate, or extract received mail content. Counted as InvalidFormat. MailId={MailId}",
            mailId);

        try
        {
            await moveFailureStore.RecordProcessedAsync(mailId, cancellationToken);
            await receivedMailSession.MoveToProcessedMailboxAsync(mailId, cancellationToken);
            await moveFailureStore.RemoveAsync(mailId, cancellationToken);
            logger.LogInformation(
                "Moved invalid format message to processed mailbox. MailId={MailId}",
                mailId);
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
                "Invalid format message was counted but moving mail to processed mailbox failed. MailId={MailId}",
                mailId);
        }
    }

    /// <summary>
    /// 受信メールを検証して連携項目を抽出します。
    /// エラーがある場合は、すべてのエラーをまとめて通知します。
    /// </summary>
    /// <summary>
    /// ValidateAndExtractAsyncを実行します。
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

    /// <summary>
    /// メール単位の入力不備を表します。
    /// </summary>
    private class ReceivedMailProcessingException(IReadOnlyCollection<string> errors)
        : Exception(string.Join(Environment.NewLine, errors))
    {
        public IReadOnlyList<string> Errors { get; } = errors.ToArray();
    }
}
