using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
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
        FakeRunStatusNotifier notifier = new();
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
    }

    private sealed class FakeJobExecutionLock(JobExecutionLockHandle? handle) : IJobExecutionLock
    {
        public JobExecutionLockHandle? TryAcquire() => handle;
    }

    private sealed class FakeRunStatusNotifier : IRunStatusNotifier
    {
        public List<(BatchRunResult Result, int ExitCode)> Notifications { get; } = [];

        public Task NotifyAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default)
        {
            Notifications.Add((result, exitCode));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceivedMailPipeline : IReceivedMailPipeline
    {
        public bool Processed
        {
            get; private set;
        }

        public Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
        {
            Processed = true;
            return Task.FromResult(new ProcessResult(targetMailIds.Count));
        }
    }

    private sealed class FakeReceivedMailSession : IReceivedMailSession
    {
        public bool Connected
        {
            get; private set;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ReceivedMailId>>([]);

        public Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
