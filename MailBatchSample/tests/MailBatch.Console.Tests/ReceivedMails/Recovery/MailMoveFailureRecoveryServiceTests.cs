using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Recovery;

public sealed class MailMoveFailureRecoveryServiceTests
{

    // 目的: 保存済みの移動失敗を復旧して記録を消せることを確認する。
    // 前提・入力: 処理済み移動とエラー移動の失敗記録を各1件渡す。
    // 期待結果: 各メールが対応するフォルダーへ移動され、失敗記録がすべて削除される。
    // 検知したい異常: 移動先の取り違えまたは復旧済み記録の残存。
    [Fact]
    public async Task RecoverAsync_WhenMoveFailureRecordsExist_MovesMailsAndRemovesRecords()
    {
        ReceivedMailId processedMailId = new(10, 999);
        ReceivedMailId errorMailId = new(11, 999);
        FakeReceivedMailMover session = new();
        FakeMoveFailureStore moveFailureStore = new();
        moveFailureStore.Failures.Add(CreateFailure(processedMailId, MailMoveFailureDestination.Processed));
        moveFailureStore.Failures.Add(CreateFailure(errorMailId, MailMoveFailureDestination.Error));
        MailMoveFailureRecoveryService service = new(
            session,
            moveFailureStore,
            new FakeStateMetricAlertMonitor(),
            NullLogger<MailMoveFailureRecoveryService>.Instance);

        await service.RecoverAsync(CancellationToken.None);

        Assert.Equal(processedMailId, session.ProcessedMailIds.Single());
        Assert.Equal(errorMailId, session.ErrorMailIds.Single());
        Assert.Empty(moveFailureStore.Failures);
    }

    // 目的: 移動の再失敗時に記録を保持して日時を更新することを確認する。
    // 前提・入力: 移動時に例外を送出するメール移動サービスと既存失敗記録を渡す。
    // 期待結果: 失敗記録は削除されず、最終失敗日時が更新される。
    // 検知したい異常: 再失敗した記録を削除して復旧対象を失う不具合。
    [Fact]
    public async Task RecoverAsync_WhenMoveFails_RecordsLatestFailureAndRetainsRecord()
    {
        ReceivedMailId mailId = new(12, 999);
        FakeReceivedMailMover session = new()
        {
            FailProcessedMove = true
        };
        FakeMoveFailureStore moveFailureStore = new();
        MailMoveFailure failure = CreateFailure(mailId, MailMoveFailureDestination.Processed);
        moveFailureStore.Failures.Add(failure);
        FakeStateMetricAlertMonitor notifier = new();
        MailMoveFailureRecoveryService service = new(
            session,
            moveFailureStore,
            notifier,
            NullLogger<MailMoveFailureRecoveryService>.Instance);

        await service.RecoverAsync(CancellationToken.None);

        Assert.Equal(failure, Assert.Single(moveFailureStore.RecoveryFailures));
        Assert.Equal(failure, Assert.Single(moveFailureStore.Failures));
        Assert.Equal(failure, Assert.Single(notifier.Failures));
    }

    private static MailMoveFailure CreateFailure(ReceivedMailId mailId, MailMoveFailureDestination destination) =>
        new(mailId, destination, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeReceivedMailMover : IReceivedMailMover
    {
        public bool FailProcessedMove
        {
            get; init;
        }

        public List<ReceivedMailId> ProcessedMailIds { get; } = [];

        public List<ReceivedMailId> ErrorMailIds { get; } = [];


        public Task<ReceivedMailId?> MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (FailProcessedMove)
            {
                throw new InvalidOperationException("Move failed.");
            }

            ProcessedMailIds.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(mailId);
        }

        public Task<ReceivedMailId?> MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            ErrorMailIds.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(mailId);
        }
    }

    private sealed class FakeMoveFailureStore : IMailMoveFailureStore
    {
        public List<MailMoveFailure> Failures { get; } = [];

        public List<MailMoveFailure> RecoveryFailures { get; } = [];

        public Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MailMoveFailure>>(Failures.ToArray());

        public Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RecordRecoveryFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken = default)
        {
            RecoveryFailures.Add(failure);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default)
        {
            _ = Failures.Remove(failure);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeStateMetricAlertMonitor : IStateMetricAlertMonitor
    {
        public List<MailMoveFailure> Failures { get; } = [];

        public Task<bool> TryCheckMailMoveStagnationAsync(
            IReadOnlyList<MailMoveFailure> failures,
            CancellationToken cancellationToken = default)
        {
            Failures.AddRange(failures);
            return Task.FromResult(true);
        }
    }
}
