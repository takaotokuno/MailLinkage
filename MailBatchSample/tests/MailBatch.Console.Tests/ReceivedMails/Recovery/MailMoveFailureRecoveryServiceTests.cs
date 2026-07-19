using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.Searching;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Recovery;

public sealed class MailMoveFailureRecoveryServiceTests
{
    [Fact]
    public async Task RecoverAsync_WhenMoveFailureRecordsExist_MovesMailsAndRemovesRecords()
    {
        ReceivedMailId processedMailId = new(10, 999);
        ReceivedMailId errorMailId = new(11, 999);
        FakeReceivedMailSession session = new();
        FakeMoveFailureStore moveFailureStore = new();
        moveFailureStore.Failures.Add(CreateFailure(processedMailId, MailMoveFailureDestination.Processed));
        moveFailureStore.Failures.Add(CreateFailure(errorMailId, MailMoveFailureDestination.Error));
        MailMoveFailureRecoveryService service = new(
            session,
            moveFailureStore,
            NullLogger<MailMoveFailureRecoveryService>.Instance);

        await service.RecoverAsync(CancellationToken.None);

        Assert.Equal(processedMailId, session.ProcessedMailIds.Single());
        Assert.Equal(errorMailId, session.ErrorMailIds.Single());
        Assert.Empty(moveFailureStore.Failures);
    }

    [Fact]
    public async Task RecoverAsync_WhenMoveFails_RecordsLatestFailureAndRetainsRecord()
    {
        ReceivedMailId mailId = new(12, 999);
        FakeReceivedMailSession session = new() { FailProcessedMove = true };
        FakeMoveFailureStore moveFailureStore = new();
        MailMoveFailure failure = CreateFailure(mailId, MailMoveFailureDestination.Processed);
        moveFailureStore.Failures.Add(failure);
        MailMoveFailureRecoveryService service = new(
            session,
            moveFailureStore,
            NullLogger<MailMoveFailureRecoveryService>.Instance);

        await service.RecoverAsync(CancellationToken.None);

        Assert.Equal(failure, Assert.Single(moveFailureStore.RecoveryFailures));
        Assert.Equal(failure, Assert.Single(moveFailureStore.Failures));
    }

    private static MailMoveFailure CreateFailure(ReceivedMailId mailId, MailMoveFailureDestination destination) =>
        new(mailId, destination, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeReceivedMailSession : IReceivedMailSession
    {
        public bool FailProcessedMove { get; init; }

        public List<ReceivedMailId> ProcessedMailIds { get; } = [];

        public List<ReceivedMailId> ErrorMailIds { get; } = [];

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (FailProcessedMove)
            {
                throw new InvalidOperationException("Move failed.");
            }

            ProcessedMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            ErrorMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeMoveFailureStore : IProcessedMailMoveFailureStore
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
}
