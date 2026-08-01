using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing;

public sealed class BatchRunCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_WhenRunCompletesNormally_DeletesExpiredData()
    {
        FakeBatchDataRetentionService dataRetentionService = new();
        BatchRunCompletionService service = CreateService(dataRetentionService);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await service.CompleteAsync(new BatchRunResult(new ProcessResult(Total: 0), now, now), 0);

        Assert.True(dataRetentionService.DeleteAttempted);
    }

    [Fact]
    public async Task CompleteAsync_WhenRunHasFatalError_DoesNotDeleteExpiredData()
    {
        FakeBatchDataRetentionService dataRetentionService = new();
        BatchRunCompletionService service = CreateService(dataRetentionService);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        BatchRunResult result = new(
            new ProcessResult(Total: 0),
            now,
            now,
            new FatalBatchError("Failure", "The batch failed.", "Processing"));

        await service.CompleteAsync(result, 1);

        Assert.False(dataRetentionService.DeleteAttempted);
    }

    private static BatchRunCompletionService CreateService(IBatchDataRetentionService dataRetentionService) => new(
        new BatchRunContext("run-completion-test"),
        new FakeBatchRunHistoryStore(),
        new FakeHistoricalMetricAlertMonitor(),
        new FakeRunStatusNotifier(),
        dataRetentionService,
        NullLogger<BatchRunCompletionService>.Instance);

    private sealed class FakeBatchRunHistoryStore : IBatchRunHistoryStore
    {
        public Task AddAsync(BatchRunHistory history, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BatchRunHistory>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BatchRunHistory>>([]);
    }

    private sealed class FakeHistoricalMetricAlertMonitor : IHistoricalMetricAlertMonitor
    {
        public Task<bool> TryCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeRunStatusNotifier : IRunStatusNotifier
    {
        public Task<bool> TryNotifyAsync(
            BatchRunResult result,
            int exitCode,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeBatchDataRetentionService : IBatchDataRetentionService
    {
        public bool DeleteAttempted { get; private set; }

        public void TryDeleteExpiredData() => DeleteAttempted = true;
    }
}
