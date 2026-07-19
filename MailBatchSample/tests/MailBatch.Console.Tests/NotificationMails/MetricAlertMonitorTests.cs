using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.State;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class MetricAlertMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StateMonitor_WhenUnrecoveredForSevenDays_SendsAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        StateMetricAlertMonitor monitor = new(notifier, new FixedTimeProvider(Now));
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            Now.AddDays(-7),
            Now);

        bool notified = await monitor.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        (string title, string message) = Assert.Single(notifier.Alerts);
        Assert.Equal("Stalled mail moves", title);
        Assert.Contains("999:123", message);
    }

    [Fact]
    public async Task StateMonitor_WhenFailureIsNewerThanSevenDays_DoesNotSendAlert()
    {
        FakeMetricAlertNotifier notifier = new();
        StateMetricAlertMonitor monitor = new(notifier, new FixedTimeProvider(Now));
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            Now.AddDays(-7).AddTicks(1),
            Now);

        bool notified = await monitor.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        Assert.Empty(notifier.Alerts);
    }

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

    private static IReadOnlyList<BatchRunHistory> CreateHistory(int longRunCount) => Enumerable.Range(0, 10)
        .Select(index =>
        {
            TimeSpan duration = index < longRunCount ? TimeSpan.FromHours(1).Add(TimeSpan.FromTicks(1)) : TimeSpan.FromHours(1);
            return new BatchRunHistory($"run-{index}", Now - duration, Now, 0, 0, 0, 0, 0, null, null);
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
