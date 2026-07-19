using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.NotificationMails;

/// <summary>
/// 運用メトリクスをチェックし、しきい値を超えた場合に管理者へアラート通知します。
/// </summary>
internal interface IMetricAlertMonitor
{
    Task<bool> TryCheckMailMoveStagnationAsync(
        IReadOnlyList<MailMoveFailure> failures,
        CancellationToken cancellationToken = default);
}

internal sealed class MetricAlertMonitor(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    TimeProvider timeProvider,
    ILogger<MetricAlertMonitor> logger) : IMetricAlertMonitor
{
    internal const int THRESHOLD_DAYS = 7;

    /// <summary>
    /// 復旧できていないメール移動の滞留期間をチェックします。
    /// </summary>
    public async Task<bool> TryCheckMailMoveStagnationAsync(
        IReadOnlyList<MailMoveFailure> failures,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset threshold = timeProvider.GetUtcNow().AddDays(-THRESHOLD_DAYS);
        MailMoveFailure[] stalledFailures = failures
            .Where(failure => failure.CreatedAtUtc <= threshold)
            .ToArray();
        if (stalledFailures.Length == 0)
        {
            return true;
        }

        string details = string.Join(Environment.NewLine, stalledFailures.Select(failure =>
            $"- MailId={failure.MailId}, Destination={failure.Destination}, CreatedAt={failure.CreatedAtUtc:O}, LastFailedAt={failure.LastFailedAtUtc:O}"));
        string message = $"The following mail moves remain unrecovered for {THRESHOLD_DAYS} days or more. Please investigate the mailbox and batch logs."
            + $"{Environment.NewLine}Count: {stalledFailures.Length}{Environment.NewLine}{details}";

        return await TrySendAlertAsync(
            "Stalled mail moves",
            message,
            cancellationToken);
    }

    /// <summary>
    /// チェック固有のアラート内容を共通テンプレートへ適用して送信します。
    /// </summary>
    private async Task<bool> TrySendAlertAsync(
        string alertTitle,
        string alertMessage,
        CancellationToken cancellationToken)
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
