using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
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
        MailFetchQueueProducer producer = CreateProducer(
            _ =>
            {
                throw new ReceivedMailFormatException("MIME message is damaged.");
            });

        ProcessResult result = await producer.ProduceAsync([new ReceivedMailId(1, 999)]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
    }

    [Fact]
    public async Task ProduceAsync_WhenExtractKeyIsMissing_CountsAsInvalidFormat()
    {
        MailFetchQueueProducer producer = CreateProducer(
            id =>
            {
                return new ReceivedMail(id, "sender@example.com", "subject", "body without key");
            });

        ProcessResult result = await producer.ProduceAsync([new ReceivedMailId(1, 999)]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
    }

    [Fact]
    public async Task ProduceAsync_WhenDataSizeLimitIsExceeded_CountsAsInvalidFormat()
    {
        MailFetchQueueProducer producer = CreateProducer(
            id =>
            {
                return new ReceivedMail(
                                id,
                                "sender@example.com",
                                new string('s', ReceivedMail.MaxSubjectLength + 1),
                                $"Key: ABC123{Environment.NewLine}{new string('b', ReceivedMail.MaxBodyLength + 1)}");
            });

        ProcessResult result = await producer.ProduceAsync([new ReceivedMailId(1, 999)]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
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

        await Assert.ThrowsAsync<ChannelClosedException>(() => producer.ProduceAsync([new ReceivedMailId(1, 999)]));
    }

    [Fact]
    public async Task ProduceAsync_WhenFetchFailsWithNonMimeError_ThrowsSystemError()
    {
        MailFetchQueueProducer producer = CreateProducer(
            _ =>
            {
                throw new InvalidOperationException("IMAP server is unavailable.");
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => producer.ProduceAsync([new ReceivedMailId(1, 999)]));
    }

    private static MailFetchQueueProducer CreateProducer(
        Func<ReceivedMailId, ReceivedMail> mailFactory,
        ChannelWriter<MailLinkageRequest>? writer = null)
    {
        Channel<MailLinkageRequest> channel = Channel.CreateUnbounded<MailLinkageRequest>();
        return new MailFetchQueueProducer(
            new FakeReceivedMailSession(mailFactory),
            writer ?? channel.Writer,
            new FakeMailNotifier(),
            new MailNotificationFactory(CreateNotificationOptions(), new BatchRunContext("test-run")),
            new EmptyMoveFailureStore(),
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
                    Name = MailNotificationOptions.RunStatusTemplateName,
                    Subject = "Run {RunId} {Status}",
                    Body = "Exit={ExitCode} Total={Total} Succeeded={Succeeded} InvalidFormat={InvalidFormat} ApiFailed={ApiFailed} Fatal={FatalErrorCode} {FatalErrorMessage} {FatalErrorStage}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.ValidationErrorTemplateName,
                    Subject = "Validation {MailId} {Subject}",
                    Body = "Errors:\n{ValidationErrors}"
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
        public Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyMoveFailureStore : IProcessedMailMoveFailureStore
    {
        public Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMailNotifier : IMailNotifier
    {
        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
