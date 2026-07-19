using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Locking;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.Searching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing;

public sealed class BatchRunnerTests
{
    /// <summary>
    /// 状態: 実行ロックを取得できず、多重起動が検知される。
    /// 振る舞い: IMAP接続やパイプライン処理を行わず、致命的エラー通知を送信して終了コード1を返す。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenExecutionLockIsAlreadyHeld_SendsFatalErrorNotificationAndReturnsExitCode1()
    {
        DateTimeOffset beforeRun = DateTimeOffset.UtcNow;
        FakeBatchRunCompletionService notifier = new();
        FakeReceivedMailSession session = new();
        FakeReceivedMailPipeline pipeline = new();
        BatchRunner runner = new(
            new ImapOptions(),
            new ApiOptions(),
            new BatchOptions(),
            new MailSearchOptions(),
            new BatchRunContext("run-duplicate"),
            NullLogger<BatchRunner>.Instance,
            pipeline,
            notifier,
            session,
            new FakeMailMoveFailureRecoveryService(),
            new FakeJobExecutionLock(null));

        int exitCode = await runner.RunAsync();

        Assert.Equal(1, exitCode);
        Assert.False(session.Connected);
        Assert.False(pipeline.Processed);
        _ = Assert.Single(notifier.Notifications);
        Assert.Equal(new ProcessResult(Total: 0), notifier.Notifications[0].Result.ProcessResult);
        Assert.Equal(new FatalBatchError(
            Code: "DuplicateRun",
            Message: "Another mail batch instance is already running.",
            Stage: "Startup"), notifier.Notifications[0].Result.FatalError);
        Assert.Equal(1, notifier.Notifications[0].ExitCode);
        Assert.InRange(notifier.Notifications[0].Result.StartedAt, beforeRun, DateTimeOffset.UtcNow);
        Assert.InRange(
            notifier.Notifications[0].Result.EndedAt,
            notifier.Notifications[0].Result.StartedAt,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 状態: 実行ロックを取得できず、多重起動が検知される。
    /// 振る舞い: 完了処理を実行し、多重起動の終了コード1を返す。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenDuplicateRunIsDetected_ReturnsOriginalExitCode()
    {
        FakeBatchRunCompletionService notifier = new();
        BatchRunner runner = new(
            new ImapOptions(),
            new ApiOptions(),
            new BatchOptions(),
            new MailSearchOptions(),
            new BatchRunContext("run-duplicate"),
            NullLogger<BatchRunner>.Instance,
            new FakeReceivedMailPipeline(),
            notifier,
            new FakeReceivedMailSession(),
            new FakeMailMoveFailureRecoveryService(),
            new FakeJobExecutionLock(null));

        int exitCode = await runner.RunAsync();

        Assert.Equal(1, exitCode);
        _ = Assert.Single(notifier.Notifications);
    }

    /// <summary>
    /// 状態: IMAP接続時に例外が発生する。
    /// 振る舞い: 致命的エラー通知を送信してから例外を再スローする。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenConnectThrows_SendsFatalErrorNotificationAndRethrows()
    {
        InvalidOperationException exception = new("Authentication failed.");
        FakeBatchRunCompletionService notifier = new();
        FakeReceivedMailSession session = new(connectException: exception);
        BatchRunner runner = new(
            new ImapOptions(),
            new ApiOptions(),
            new BatchOptions(),
            new MailSearchOptions(),
            new BatchRunContext("run-connect-fatal"),
            NullLogger<BatchRunner>.Instance,
            new FakeReceivedMailPipeline(),
            notifier,
            session,
            new FakeMailMoveFailureRecoveryService(),
            new FakeJobExecutionLock(new JobExecutionLockHandle(new FakeLockRelease())));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            return runner.RunAsync();
        });

        Assert.Same(exception, thrown);
        _ = Assert.Single(notifier.Notifications);
        Assert.Equal(1, notifier.Notifications[0].ExitCode);
        Assert.Equal(new FatalBatchError(
            Code: nameof(InvalidOperationException),
            Message: "Authentication failed.",
            Stage: "Connection"), notifier.Notifications[0].Result.FatalError);
    }

    /// <summary>
    /// 状態: メール検索やProducer/Consumerを含む処理中に例外が発生する。
    /// 振る舞い: Processing段階の致命的エラーとして通知してから例外を再スローする。
    /// </summary>
    [Fact]
    public async Task RunAsync_WhenUseCaseThrows_SendsFatalErrorNotificationAndRethrows()
    {
        ApplicationException exception = new("Producer stopped unexpectedly.");
        FakeBatchRunCompletionService notifier = new();
        FakeReceivedMailPipeline pipeline = new(processException: exception);
        BatchRunner runner = new(
            new ImapOptions(),
            new ApiOptions(),
            new BatchOptions(),
            new MailSearchOptions(),
            new BatchRunContext("run-processing-fatal"),
            NullLogger<BatchRunner>.Instance,
            pipeline,
            notifier,
            new FakeReceivedMailSession(mailIds: [new ReceivedMailId(1, 1)]),
            new FakeMailMoveFailureRecoveryService(),
            new FakeJobExecutionLock(new JobExecutionLockHandle(new FakeLockRelease())));

        ApplicationException thrown = await Assert.ThrowsAsync<ApplicationException>(() =>
        {
            return runner.RunAsync();
        });

        Assert.Same(exception, thrown);
        Assert.True(pipeline.Processed);
        _ = Assert.Single(notifier.Notifications);
        Assert.Equal(1, notifier.Notifications[0].ExitCode);
        Assert.Equal(new FatalBatchError(
            Code: nameof(ApplicationException),
            Message: "Producer stopped unexpectedly.",
            Stage: "Processing"), notifier.Notifications[0].Result.FatalError);
    }


    [Fact]
    public async Task RunAsync_WhenMoveFailureRecordsExist_RecoversBeforeSearchingMessages()
    {
        ReceivedMailId processedMailId = new(10, 999);
        ReceivedMailId errorMailId = new(11, 999);
        FakeBatchRunCompletionService notifier = new();
        FakeReceivedMailSession session = new(mailIds: [processedMailId, errorMailId]);
        FakeMailMoveFailureRecoveryService recoveryService = new(session.MarkRecoveryCompleted);
        BatchRunner runner = new(
            new ImapOptions(),
            new ApiOptions(),
            new BatchOptions(),
            new MailSearchOptions(),
            new BatchRunContext("run-recover-move-failures"),
            NullLogger<BatchRunner>.Instance,
            new FakeReceivedMailPipeline(),
            notifier,
            session,
            recoveryService,
            new FakeJobExecutionLock(new JobExecutionLockHandle(new FakeLockRelease())));

        int exitCode = await runner.RunAsync();

        Assert.Equal(0, exitCode);
        Assert.True(recoveryService.Recovered);
        Assert.True(session.SearchedAfterRecovery);
        _ = Assert.Single(notifier.Notifications);
        Assert.True(notifier.Notifications[0].Result.EndedAt >= notifier.Notifications[0].Result.StartedAt);
    }

    private sealed class FakeLockRelease : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private sealed class FakeJobExecutionLock(JobExecutionLockHandle? handle) : IJobExecutionLock
    {
        public JobExecutionLockHandle? TryAcquire() => handle;
    }

    private sealed class FakeBatchRunCompletionService : IBatchRunCompletionService
    {
        public List<(BatchRunResult Result, int ExitCode)> Notifications { get; } = [];

        public Task CompleteAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default)
        {
            Notifications.Add((result, exitCode));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceivedMailPipeline(Exception? processException = null) : IReceivedMailPipeline
    {
        public bool Processed
        {
            get; private set;
        }

        public Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
        {
            Processed = true;

            return processException is not null ? throw processException : Task.FromResult(new ProcessResult(targetMailIds.Count));
        }
    }

    private sealed class FakeReceivedMailSession(Exception? connectException = null, IReadOnlyList<ReceivedMailId>? mailIds = null) : IReceivedMailSession
    {
        private bool _recoveryCompleted;

        public bool Connected
        {
            get; private set;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (connectException is not null)
            {
                throw connectException;
            }

            Connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool SearchedAfterRecovery
        {
            get; private set;
        }

        public Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default)
        {
            SearchedAfterRecovery = _recoveryCompleted;
            return Task.FromResult(mailIds ?? []);
        }

        public Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public List<ReceivedMailId> ProcessedMailIds { get; } = [];

        public List<ReceivedMailId> ErrorMailIds { get; } = [];

        public Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            ProcessedMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            ErrorMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public void MarkRecoveryCompleted() => _recoveryCompleted = true;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeMailMoveFailureRecoveryService(Action? onRecover = null) : IMailMoveFailureRecoveryService
    {
        public bool Recovered
        {
            get; private set;
        }

        public Task RecoverAsync(CancellationToken cancellationToken)
        {
            Recovered = true;
            onRecover?.Invoke();
            return Task.CompletedTask;
        }
    }


}
