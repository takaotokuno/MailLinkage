using MailBatch.Console.BatchProcessing.History;

namespace MailBatch.Console.NotificationMails;

internal interface IHistoricalMetricAlertMonitor
{
    Task<bool> TryCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 蓄積したバッチ実行履歴を対象にアラートを評価します。
/// </summary>
internal sealed class HistoricalMetricAlertMonitor(
    IBatchRunHistoryStore historyStore,
    IMetricAlertNotifier alertNotifier) : IHistoricalMetricAlertMonitor
{
    internal const int RUN_COUNT = 10;
    internal static readonly TimeSpan DurationThreshold = TimeSpan.FromHours(1);

    public async Task<bool> TryCheckAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BatchRunHistory> history = await historyStore.GetRecentAsync(RUN_COUNT, cancellationToken);
        if (history.Count < RUN_COUNT)
        {
            return true;
        }

        int exceededCount = history.Count(run => run.Duration > DurationThreshold);
        if (exceededCount * 2 <= RUN_COUNT)
        {
            return true;
        }

        string message = $"More than 50% of the last {RUN_COUNT} batch runs took longer than one hour."
            + $"{Environment.NewLine}Exceeded: {exceededCount}/{RUN_COUNT}";
        return await alertNotifier.TryNotifyAsync("Batch processing duration degradation", message, cancellationToken);
    }
}
