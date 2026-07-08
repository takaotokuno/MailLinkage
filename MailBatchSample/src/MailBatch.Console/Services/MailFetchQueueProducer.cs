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
                    IReadOnlyList<string> validationErrors = request.Validate();
                    if (validationErrors.Count > 0)
                    {
                        await NotifyValidationErrorAsync(request, validationErrors);
                        result.IncrementFailure();
                        logger.LogWarning(
                            "Validation failed for received mail request. MessageId={MessageId}, Errors={ValidationErrors}",
                            request.MessageId,
                            string.Join("; ", validationErrors));
                        continue;
                    }

                    await writer.WriteAsync(request);
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
                    logger.LogError(ex, "Failed to fetch, transform, validate, or queue message. Uid={Uid}", uid);
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
            return ReceivedMailMapper.ToRequest(message, summary.FirstOrDefault()?.InternalDate) with { Uid = uid };
        }
        finally
        {
            imapLock.Release();
        }
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

        string body = string.Join(Environment.NewLine, new[]
        {
            "The received mail could not be accepted because it failed validation.",
            $"MessageId: {request.MessageId}",
            $"Subject: {CreatePreview(request.Subject)}",
            "Validation errors:",
            string.Join(Environment.NewLine, validationErrors.Select(error => $"- {error}"))
        });

        await mailNotifier.SendAsync(new MailNotification(
            request.Sender,
            "Received mail validation failed",
            body));
    }

    private static string CreatePreview(string value)
    {
        const int maxPreviewLength = 200;
        return value.Length <= maxPreviewLength ? value : $"{value[..maxPreviewLength]}...";
    }
}
