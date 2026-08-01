using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Locking;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.Searching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing;

public sealed class BatchRunnerTests
{

    /// <summary>
    /// 多重起動を安全に終了できることを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: 取得済みの実行ロックを返すロックサービスでバッチを起動する。<br/>
    /// 期待結果: IMAP接続とパイプラインを実行せず、致命的エラーを通知して終了コード1を返す。<br/>
    /// 検知したい異常: ロック競合時にもメール処理を開始する、または成功終了する不具合。
    /// </remarks>
    [Fact]
    public async Task RunAsync_WhenExecutionLockIsAlreadyHeld_SendsFatalErrorNotificationAndReturnsExitCode1()
    {
        DateTimeOffset utcNow = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(utcNow);
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
            session,
            new FakeMailMoveFailureRecoveryService(),
            new FakeJobExecutionLock(null),
            timeProvider);

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
        Assert.Equal(utcNow, notifier.Notifications[0].Result.StartedAt);
        Assert.Equal(utcNow, notifier.Notifications[0].Result.EndedAt);
    }

    /// <summary>
    /// IMAP接続失敗を通知して呼び出し元へ伝播することを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: IMAP接続時に認証例外を送出するセッションでバッチを起動する。<br/>
    /// 期待結果: Connection段階の致命的エラーを1件通知し、同じ例外を再送出する。<br/>
    /// 検知したい異常: 接続例外が握り潰される、または段階を誤って通知する不具合。
    /// </remarks>
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
    /// メール処理中の例外を通知して呼び出し元へ伝播することを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: パイプライン実行時にApplicationExceptionを送出させる。<br/>
    /// 期待結果: Processing段階の致命的エラーを1件通知し、同じ例外を再送出する。<br/>
    /// 検知したい異常: 処理例外が握り潰される、またはエラー内容が通知から欠落する不具合。
    /// </remarks>
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

    /// <summary>
    /// 通常検索より先に前回の移動失敗を復旧することを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: 処理済み移動とエラー移動の失敗記録がある状態でバッチを起動する。<br/>
    /// 期待結果: 両メールの移動復旧が検索開始前に実行される。<br/>
    /// 検知したい異常: 未復旧メールを残したまま新規メール検索を開始する不具合。
    /// </remarks>
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

    private sealed class FakeReceivedMailSession(Exception? connectException = null, IReadOnlyList<ReceivedMailId>? mailIds = null) : IReceivedMailSession, IReceivedMailSearcher
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
