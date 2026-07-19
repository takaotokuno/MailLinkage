using MailBatch.Console.ReceivedMails.State;

namespace MailBatch.Console.NotificationMails;

/// <summary>未解消のメール処理状態に基づくメトリクスを監視します。</summary>
internal interface IStateMetricAlertMonitor
{
    /// <summary>長期間解消されていないメール移動失敗を評価し、通知を試みます。</summary>
    Task<bool> TryCheckMailMoveStagnationAsync(
        IReadOnlyList<MailMoveFailure> failures,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 現在解消されていない状態を対象にアラートを評価します。
/// </summary>
internal sealed class StateMetricAlertMonitor(
    IMetricAlertNotifier alertNotifier,
    TimeProvider timeProvider) : IStateMetricAlertMonitor
{
    internal const int THRESHOLD_DAYS = 7;

    /// <inheritdoc />
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

        string details = string.Join(
            Environment.NewLine,
            stalledFailures.Select(failure =>
                $"- MailId={failure.MailId}, Destination={failure.Destination}, CreatedAt={failure.CreatedAtUtc:O}, LastFailedAt={failure.LastFailedAtUtc:O}"));
        string message = $"The following mail moves remain unrecovered for {THRESHOLD_DAYS} days or more. Please investigate the mailbox and batch logs."
            + $"{Environment.NewLine}Count: {stalledFailures.Length}{Environment.NewLine}{details}";

        return await alertNotifier.TryNotifyAsync("Stalled mail moves", message, cancellationToken);
    }
}
