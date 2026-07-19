using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Searching;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.Pipeline;

public sealed class MailFetchQueueProducerTests
{
    [Fact]
    public async Task ProduceAsync_WhenFetchFailsWithMimeDamage_CountsAsInvalidFormat()
    {
        ReceivedMailId mailId = new(1, 999);
        FakeReceivedMailSession session = new(_ =>
        {
            throw new ReceivedMailFormatException("MIME message is damaged.");
        });
        MailFetchQueueProducer producer = CreateProducer(session);

        ProcessResult result = await producer.ProduceAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
        Assert.Equal(mailId, session.ProcessedMailIds.Single());
    }

    [Fact]
    public async Task ProduceAsync_WhenExtractKeyIsMissing_CountsAsInvalidFormat()
    {
        ReceivedMailId mailId = new(1, 999);
        FakeReceivedMailSession session = new(id =>
        {
            return new ReceivedMail(id, "sender@example.com", "subject", "body without key");
        });
        MailFetchQueueProducer producer = CreateProducer(session);

        ProcessResult result = await producer.ProduceAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
        Assert.Equal(mailId, session.ProcessedMailIds.Single());
    }

    [Fact]
    public async Task ProduceAsync_WhenDataSizeLimitIsExceeded_CountsAsInvalidFormat()
    {
        ReceivedMailId mailId = new(1, 999);
        FakeReceivedMailSession session = new(id =>
        {
            return new ReceivedMail(
                            id,
                            "sender@example.com",
                            new string('s', ReceivedMail.MAX_SUBJECT_LENGTH + 1),
                            $"Key: ABC123{Environment.NewLine}{new string('b', ReceivedMail.MAX_BODY_LENGTH + 1)}");
        });
        MailFetchQueueProducer producer = CreateProducer(session);

        ProcessResult result = await producer.ProduceAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
        Assert.Equal(mailId, session.ProcessedMailIds.Single());
    }

    [Fact]
    public async Task ProduceAsync_WhenQueueWriteFails_ThrowsSystemError()
    {
        Channel<MailLinkageRequest> channel = Channel.CreateBounded<MailLinkageRequest>(1);
        channel.Writer.Complete(new InvalidOperationException("Queue is unavailable."));
        MailFetchQueueProducer producer = CreateProducer(
            id =>
            {
                return new ReceivedMail(id, "sender@example.com", "subject", "Key: ABC123");
            },
            channel.Writer);

        _ = await Assert.ThrowsAsync<ChannelClosedException>(() =>
        {
            return producer.ProduceAsync([new ReceivedMailId(1, 999)]);
        });
    }

    [Fact]
    public async Task ProduceAsync_WhenFetchFailsWithNonMimeError_ThrowsSystemError()
    {
        MailFetchQueueProducer producer = CreateProducer(
            _ =>
            {
                throw new InvalidOperationException("IMAP server is unavailable.");
            });

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            return producer.ProduceAsync([new ReceivedMailId(1, 999)]);
        });
    }


    [Fact]
    public async Task ProduceAsync_WhenMoveFailureRecordExists_SkipsMailWithoutCreatingRequest()
    {
        ReceivedMailId mailId = new(9, 999);
        FakeReceivedMailSession session = new(_ =>
        {
            throw new InvalidOperationException("Request should not be created.");
        });
        FakeMoveFailureStore moveFailureStore = new([new MailMoveFailure(mailId, MailMoveFailureDestination.Error)]);
        MailFetchQueueProducer producer = CreateProducer(session, moveFailureStore: moveFailureStore);

        ProcessResult result = await producer.ProduceAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1), result);
        Assert.Empty(session.ProcessedMailIds);
    }

    private static MailFetchQueueProducer CreateProducer(
        Func<ReceivedMailId, ReceivedMail> mailFactory,
        ChannelWriter<MailLinkageRequest>? writer = null,
        FakeMoveFailureStore? stateStore = null) => CreateProducer(new FakeReceivedMailSession(mailFactory), writer, stateStore);

    private static MailFetchQueueProducer CreateProducer(
        FakeReceivedMailSession session,
        ChannelWriter<MailLinkageRequest>? writer = null,
        FakeMoveFailureStore? stateStore = null)
    {
        Channel<MailLinkageRequest> channel = Channel.CreateUnbounded<MailLinkageRequest>();
        stateStore ??= new FakeMoveFailureStore();
        return new MailFetchQueueProducer(
            session,
            writer ?? channel.Writer,
            new FakeMailNotifier(),
            new MailNotificationFactory(CreateNotificationOptions(), new BatchRunContext("test-run")),
            stateStore,
            stateStore,
            NullLogger<MailFetchQueueProducer>.Instance);
    }

    private static MailNotificationOptions CreateNotificationOptions()
    {
        return new MailNotificationOptions
        {
            AdminAddress = "admin@example.com",
            Templates =
            [
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME,
                    Subject = "Run {RunId} {Status}",
                    Body = "Exit={ExitCode} Total={Total} Succeeded={Succeeded} InvalidFormat={InvalidFormat} ApiFailed={ApiFailed} Fatal={FatalErrorCode} {FatalErrorMessage} {FatalErrorStage}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.VALIDATION_ERROR_TEMPLATE_NAME,
                    Subject = "Validation {MailId} {Subject}",
                    Body = "Errors:\n{ValidationErrors}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.METRIC_ALERT_TEMPLATE_NAME,
                    Subject = "Alert: {AlertTitle}",
                    Body = "{AlertMessage}"
                }
            ]
        };
    }

    private sealed class FakeReceivedMailSession(Func<ReceivedMailId, ReceivedMail> mailFactory) : IReceivedMailSession
    {
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(mailFactory(mailId));
        public List<ReceivedMailId> ProcessedMailIds { get; } = [];
        public Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            ProcessedMailIds.Add(mailId);
            return Task.CompletedTask;
        }
        public Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeMoveFailureStore(IEnumerable<MailMoveFailure>? failures = null) : IProcessedMailStore, IMailMoveFailureStore
    {
        public List<MailMoveFailure> Failures { get; } = failures?.ToList() ?? [];

        public HashSet<ReceivedMailId> ProcessedMailIds { get; } = [];

        Task<bool> IProcessedMailStore.ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessedMailIds.Contains(mailId));

        public Task RecordAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            _ = ProcessedMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MailMoveFailure>>(Failures.ToArray());
        public Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(Failures.Any(failure =>
        {
            return failure.MailId == mailId;
        }));
        public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            Failures.Add(new MailMoveFailure(mailId, MailMoveFailureDestination.Processed));
            return Task.CompletedTask;
        }
        public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            Failures.Add(new MailMoveFailure(mailId, MailMoveFailureDestination.Error));
            return Task.CompletedTask;
        }
        public Task RecordRecoveryFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default)
        {
            _ = Failures.Remove(failure);
            return Task.CompletedTask;
        }
        public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            _ = Failures.RemoveAll(failure =>
            {
                return failure.MailId == mailId && failure.Destination == MailMoveFailureDestination.Processed;
            });
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMailNotifier : IMailNotifier
    {
        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
