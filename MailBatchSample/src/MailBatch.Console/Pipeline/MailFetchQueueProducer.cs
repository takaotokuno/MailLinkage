using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Processing;
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
    ILogger<MailFetchQueueProducer> logger) : IMailFetchQueueProducer
{
    /// <summary>
    /// メールを取得し、API送信用データへ加工したうえで内部キューへ追加します。
    /// </summary>
    public async Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
    {
        ProcessResultAccumulator result = new();

        try
        {
            foreach (ReceivedMailId mailId in targetMailIds)
            {
                await ProduceSingleAsync(mailId, result, cancellationToken);
            }

            return result.ToResult();
        }
        finally
        {
            writer.Complete();
            logger.LogInformation(
                "Producer completed queue additions. Enqueued={Enqueued}, Failed={Failed}",
                result.Succeeded,
                result.Failed);
        }
    }

    /// <summary>
    /// 指定された受信メールIDのメールを1件処理し、処理結果を集計します。
    /// </summary>
    private async Task ProduceSingleAsync(ReceivedMailId mailId, ProcessResultAccumulator result, CancellationToken cancellationToken)
    {
        try
        {
            ReceivedMail mail = await receivedMailSession.CreateRequestAsync(mailId, cancellationToken);

            ExtractedMailItem item = await ValidateAndExtractAsync(mail, cancellationToken);

            await QueueRequestAsync(item, cancellationToken);

            result.IncrementSuccess();

            logger.LogInformation(
                "Queued API request. MailId={MailId}, QueueCount={QueueCount}, BodyLength={BodyLength}",
                mail.MailId,
                result.Succeeded,
                mail.Body?.Length ?? 0);
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
    private async Task QueueRequestAsync(ExtractedMailItem item, CancellationToken cancellationToken)
    {
        MailLinkageRequest request = new(item.MailId, item.Key, string.Empty);
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

        await mailNotifier.SendAsync(notification, cancellationToken);
    }

    private class ReceivedMailProcessingException(IReadOnlyCollection<string> errors)
        : Exception(string.Join(Environment.NewLine, errors))
    {
        public IReadOnlyList<string> Errors { get; } = errors.ToArray();
    }
}
