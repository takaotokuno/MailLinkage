using Microsoft.Extensions.Logging;

namespace MailBatch.Console.NotificationMails;

/// <summary>メトリクスの閾値超過を管理者へ通知します。</summary>
internal interface IMetricAlertNotifier
{
    /// <summary>指定されたタイトルと本文でメトリクスアラートの送信を試みます。</summary>
    Task<bool> TryNotifyAsync(string alertTitle, string alertMessage, CancellationToken cancellationToken = default);
}

/// <summary>
/// メトリクスアラートの共通テンプレート適用とメール送信を担います。
/// </summary>
internal sealed class MetricAlertNotifier(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MetricAlertNotifier> logger) : IMetricAlertNotifier
{
    /// <inheritdoc />
    public async Task<bool> TryNotifyAsync(
        string alertTitle,
        string alertMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            MailNotification notification = mailNotificationFactory.CreateMetricAlert(alertTitle, alertMessage);
            await mailNotifier.SendAsync(notification, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send metric alert. AlertTitle={AlertTitle}", alertTitle);
            return false;
        }
    }
}
