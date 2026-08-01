using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.State;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class MetricAlertMonitorTests
{
    private static readonly DateTimeOffset s_now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    // 目的: 未復旧のメール移動失敗に対する停滞アラートを確認する。
    // 前提・入力: 7日前に発生し未復旧のメール移動失敗を1件渡す。
    // 期待結果: 通知処理が成功し、タイトル「Stalled mail moves」とメールIDを含むアラートが1件送信される。
    // 検知したい異常: 長期間未復旧でも停滞アラートが送信されない不具合。
    [Fact]
    public async Task StateMonitor_WhenUnrecoveredForSevenDays_SendsAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        StateMetricAlertMonitor monitor = new(notifier, new FixedTimeProvider(s_now));
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            s_now.AddDays(-7),
            s_now);

        bool notified = await monitor.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        (string title, string message) = Assert.Single(notifier.Alerts);
        Assert.Equal("Stalled mail moves", title);
        Assert.Contains("999:123", message);
    }

    // 目的: 発生から7日未満の移動失敗が通知対象外であることを確認する。
    // 前提・入力: 6日前に発生したメール移動失敗を1件渡す。
    // 期待結果: 監視処理は成功し、アラートは1件も送信されない。
    // 検知したい異常: 通知期限前の移動失敗に誤ってアラートを送る不具合。
    [Fact]
    public async Task StateMonitor_WhenFailureIsNewerThanSevenDays_DoesNotSendAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        StateMetricAlertMonitor monitor = new(notifier, new FixedTimeProvider(s_now));
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            s_now.AddDays(-7).AddTicks(1),
            s_now);

        bool notified = await monitor.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        Assert.Empty(notifier.Alerts);
    }

    // 目的: 処理時間の悪化を履歴から検知できることを確認する。
    // 前提・入力: 直近10件中6件の実行時間が1時間を超える履歴を渡す。
    // 期待結果: タイトル「Batch processing duration degradation」と「6/10」を含むアラートが1件送信される。
    // 検知したい異常: 過半数の長時間実行を性能劣化として通知できない不具合。
    [Fact]
    public async Task HistoricalMonitor_WhenSixOfLastTenRunsExceedOneHour_SendsAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        HistoricalMetricAlertMonitor monitor = new(
            new FakeBatchRunHistoryStore(CreateHistory(longRunCount: 6)),
            notifier);

        bool notified = await monitor.TryCheckAsync();

        Assert.True(notified);
        (string title, string message) = Assert.Single(notifier.Alerts);
        Assert.Equal("Batch processing duration degradation", title);
        Assert.Contains("6/10", message);
    }

    // 目的: バッチ失敗率の悪化を履歴から検知できることを確認する。
    // 前提・入力: 直近10件中6件が失敗した履歴を渡す。
    // 期待結果: タイトル「Batch failure rate degradation」と「6/10」を含むアラートが1件送信される。
    // 検知したい異常: 過半数の失敗を失敗率悪化として通知できない不具合。
    [Fact]
    public async Task HistoricalMonitor_WhenSixOfLastTenRunsFail_SendsAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        HistoricalMetricAlertMonitor monitor = new(
            new FakeBatchRunHistoryStore(CreateHistory(longRunCount: 0, failedRunCount: 6)),
            notifier);

        bool notified = await monitor.TryCheckAsync();

        Assert.True(notified);
        (string title, string message) = Assert.Single(notifier.Alerts);
        Assert.Equal("Batch failure rate degradation", title);
        Assert.Contains("6/10", message);
    }

    // 目的: 失敗が直近実行の半数以下なら通知しないことを確認する。
    // 前提・入力: 直近10件中の失敗件数として0件または5件を渡す。
    // 期待結果: 監視処理は成功し、失敗率アラートは1件も送信されない。
    // 検知したい異常: 閾値以下の失敗率で不要なアラートを送る不具合。
    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public async Task HistoricalMonitor_WhenAtMostHalfOfLastTenRunsFail_DoesNotSendFailureRateAlert(int failedRunCount)
    {
        FakeMetricAlertNotifier notifier = new();
        HistoricalMetricAlertMonitor monitor = new(
            new FakeBatchRunHistoryStore(CreateHistory(longRunCount: 0, failedRunCount)),
            notifier);

        bool notified = await monitor.TryCheckAsync();

        Assert.True(notified);
        Assert.Empty(notifier.Alerts);
    }

    // 目的: 長時間実行が直近実行の半数以下なら通知しないことを確認する。
    // 前提・入力: 直近10件中の1時間超過件数として0件または5件を渡す。
    // 期待結果: 監視処理は成功し、処理時間アラートは1件も送信されない。
    // 検知したい異常: 閾値以下の長時間実行数で不要なアラートを送る不具合。
    [Theory]
    [InlineData(5)]
    [InlineData(0)]
    public async Task HistoricalMonitor_WhenAtMostHalfOfLastTenRunsExceedOneHour_DoesNotSendAlert(int longRunCount)
    {
        FakeMetricAlertNotifier notifier = new();
        HistoricalMetricAlertMonitor monitor = new(
            new FakeBatchRunHistoryStore(CreateHistory(longRunCount)),
            notifier);

        bool notified = await monitor.TryCheckAsync();

        Assert.True(notified);
        Assert.Empty(notifier.Alerts);
    }

    // 目的: 履歴不足時に傾向判定を行わないことを確認する。
    // 前提・入力: 実行履歴を9件だけ渡す。
    // 期待結果: 監視処理は成功し、アラートは1件も送信されない。
    // 検知したい異常: 必要件数未満の履歴から誤って劣化を通知する不具合。
    [Fact]
    public async Task HistoricalMonitor_WhenFewerThanTenRunsExist_DoesNotSendAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        HistoricalMetricAlertMonitor monitor = new(
            new FakeBatchRunHistoryStore(CreateHistory(longRunCount: 6).Take(9).ToArray()),
            notifier);

        _ = await monitor.TryCheckAsync();

        Assert.Empty(notifier.Alerts);
    }

    private static IReadOnlyList<BatchRunHistory> CreateHistory(int longRunCount, int failedRunCount = 0) => Enumerable.Range(0, 10)
        .Select(index =>
        {
            TimeSpan duration = index < longRunCount ? TimeSpan.FromHours(1).Add(TimeSpan.FromTicks(1)) : TimeSpan.FromHours(1);
            int exitCode = index < failedRunCount ? 1 : 0;
            return new BatchRunHistory($"run-{index}", s_now - duration, s_now, exitCode, 0, 0, 0, 0, null, null);
        })
        .ToArray();

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeBatchRunHistoryStore(IReadOnlyList<BatchRunHistory> history) : IBatchRunHistoryStore
    {
        public Task AddAsync(BatchRunHistory item, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BatchRunHistory>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchRunHistory>>(history.Take(count).ToArray());
    }

    private sealed class FakeMetricAlertNotifier : IMetricAlertNotifier
    {
        public List<(string Title, string Message)> Alerts { get; } = [];

        public Task<bool> TryNotifyAsync(string alertTitle, string alertMessage, CancellationToken cancellationToken = default)
        {
            Alerts.Add((alertTitle, alertMessage));
            return Task.FromResult(true);
        }
    }
}
