using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.Pipeline;

public sealed class RequestQueueConsumerTests
{
    [Fact]
    public async Task ConsumeAsync_WhenApiReturnsFailure_MovesMailToErrorMailboxAndCountsFailure()
    {
        MailLinkageRequest request = new(new ReceivedMailId(1, 999), "key", "message");
        ChannelReader<MailLinkageRequest> reader = CreateCompletedReader(request);
        FakeReceivedMailMover session = new();
        FakeApiClient apiClient = new(new ApiPostResult(false, 500, "server error"));
        FakeMoveFailureStore moveFailureStore = new();
        RequestQueueConsumer consumer = new(
            new ApiOptions { Endpoint = "/api/received-mails" },
            session,
            apiClient,
            reader,
            moveFailureStore,
            moveFailureStore,
            new FakeApiExecutionResultStore(),
            NullLogger<MailLinkageRequest>.Instance);

        ProcessResult result = await consumer.ConsumeAsync();

        Assert.Equal(1, result.ApiFailed);
        Assert.Equal(0, result.Succeeded);
        Assert.Equal(request.MailId, session.ErrorMailIds.Single());
        Assert.Empty(session.ProcessedMailIds);
    }

    [Fact]
    public async Task ConsumeAsync_WhenApiReturnsSuccess_MovesMailToProcessedMailboxAndCountsSuccess()
    {
        MailLinkageRequest request = new(new ReceivedMailId(2, 999), "key", "message");
        ChannelReader<MailLinkageRequest> reader = CreateCompletedReader(request);
        FakeReceivedMailMover session = new();
        FakeApiClient apiClient = new(new ApiPostResult(true, 201, /*lang=json,strict*/ "{\"id\":1}"));
        FakeMoveFailureStore moveFailureStore = new();
        FakeApiExecutionResultStore executionResultStore = new();
        RequestQueueConsumer consumer = new(
            new ApiOptions { Endpoint = "/api/received-mails" },
            session,
            apiClient,
            reader,
            moveFailureStore,
            moveFailureStore,
            executionResultStore,
            NullLogger<MailLinkageRequest>.Instance);

        ProcessResult result = await consumer.ConsumeAsync();

        Assert.Equal(0, result.ApiFailed);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(request.MailId, session.ProcessedMailIds.Single());
        Assert.Empty(session.ErrorMailIds);
        Assert.Equal(new ReceivedMailId(1002, 200), Assert.Single(executionResultStore.MovedMailIds).MailId);
    }


    [Fact]
    public async Task ConsumeAsync_WhenProcessedMoveFailsAfterApiSuccess_RecordsFailureAndCountsApiFailure()
    {
        MailLinkageRequest request = new(new ReceivedMailId(3, 999), "key", "message");
        ChannelReader<MailLinkageRequest> reader = CreateCompletedReader(request);
        FakeReceivedMailMover session = new()
        {
            ThrowOnMoveToProcessed = true
        };
        FakeApiClient apiClient = new(new ApiPostResult(true, 201, /*lang=json,strict*/ "{\"id\":1}"));
        FakeMoveFailureStore moveFailureStore = new();
        RequestQueueConsumer consumer = new(
            new ApiOptions { Endpoint = "/api/received-mails" },
            session,
            apiClient,
            reader,
            moveFailureStore,
            moveFailureStore,
            new FakeApiExecutionResultStore(),
            NullLogger<MailLinkageRequest>.Instance);

        ProcessResult result = await consumer.ConsumeAsync();

        Assert.Equal(1, result.ApiFailed);
        Assert.Equal(0, result.Succeeded);
        Assert.Contains(request.MailId, moveFailureStore.MailIds);
    }


    [Fact]
    public async Task ConsumeAsync_WhenErrorMoveFailsAfterApiFailure_RecordsFailureAndCountsApiFailure()
    {
        MailLinkageRequest request = new(new ReceivedMailId(5, 999), "key", "message");
        ChannelReader<MailLinkageRequest> reader = CreateCompletedReader(request);
        FakeReceivedMailMover session = new()
        {
            ThrowOnMoveToError = true
        };
        FakeApiClient apiClient = new(new ApiPostResult(false, 500, "server error"));
        FakeMoveFailureStore moveFailureStore = new();
        RequestQueueConsumer consumer = new(
            new ApiOptions { Endpoint = "/api/received-mails" },
            session,
            apiClient,
            reader,
            moveFailureStore,
            moveFailureStore,
            new FakeApiExecutionResultStore(),
            NullLogger<MailLinkageRequest>.Instance);

        ProcessResult result = await consumer.ConsumeAsync();

        Assert.Equal(1, result.ApiFailed);
        Assert.Equal(0, result.Succeeded);
        Assert.Contains(request.MailId, moveFailureStore.ErrorMoveFailureMailIds);
        Assert.Empty(session.ErrorMailIds);
    }

    [Fact]
    public async Task ConsumeAsync_DoesNotWriteRequestMessageBodyToApiSendLog()
    {
        const string SENSITIVE_MESSAGE = "Key: ABC123\nName: Taro Yamada\nPhone: 090-0000-0000";
        MailLinkageRequest request = new(new ReceivedMailId(4, 999), "key", SENSITIVE_MESSAGE);
        ChannelReader<MailLinkageRequest> reader = CreateCompletedReader(request);
        FakeReceivedMailMover session = new();
        FakeApiClient apiClient = new(new ApiPostResult(true, 201, /*lang=json,strict*/ "{\"id\":1}"));
        FakeMoveFailureStore moveFailureStore = new();
        FakeLogger<MailLinkageRequest> logger = new();
        RequestQueueConsumer consumer = new(
            new ApiOptions { Endpoint = "/api/received-mails" },
            session,
            apiClient,
            reader,
            moveFailureStore,
            moveFailureStore,
            new FakeApiExecutionResultStore(),
            logger);

        _ = await consumer.ConsumeAsync();

        Assert.DoesNotContain(logger.Entries, entry =>
        {
            return entry.Contains(SENSITIVE_MESSAGE, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(logger.Entries, entry =>
        {
            return entry.Contains("Taro Yamada", StringComparison.Ordinal);
        });
        Assert.Contains(logger.Entries, entry =>
        {
            return entry.Contains($"MessageLength={SENSITIVE_MESSAGE.Length}", StringComparison.Ordinal);
        });
        Assert.Contains(logger.EntriesWithLevels, entry =>
        {
            return entry.Level == LogLevel.Information
                && entry.Message.Contains("API post succeeded", StringComparison.Ordinal);
        });
        Assert.DoesNotContain(logger.EntriesWithLevels, entry =>
        {
            return entry.Level == LogLevel.Information
                && (entry.Message.Contains("Posting queued API request", StringComparison.Ordinal)
                    || entry.Message.Contains("API execution result recorded", StringComparison.Ordinal));
        });
    }

    private static ChannelReader<MailLinkageRequest> CreateCompletedReader(MailLinkageRequest request)
    {
        Channel<MailLinkageRequest> channel = Channel.CreateUnbounded<MailLinkageRequest>();
        Assert.True(channel.Writer.TryWrite(request));
        channel.Writer.Complete();
        return channel.Reader;
    }

    private sealed class FakeApiClient(ApiPostResult result) : IApiClient
    {
        public Task<ApiPostResult> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class FakeReceivedMailMover : IReceivedMailMover
    {
        public List<ReceivedMailId> ProcessedMailIds { get; } = [];

        public List<ReceivedMailId> ErrorMailIds { get; } = [];

        public bool ThrowOnMoveToProcessed
        {
            get; init;
        }

        public bool ThrowOnMoveToError
        {
            get; init;
        }


        public Task<ReceivedMailId?> MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnMoveToProcessed)
            {
                throw new InvalidOperationException("move failed");
            }

            ProcessedMailIds.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(new ReceivedMailId(mailId.Uid + 1000, 200));
        }

        public Task<ReceivedMailId?> MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnMoveToError)
            {
                throw new InvalidOperationException("error move failed");
            }

            ErrorMailIds.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(new ReceivedMailId(mailId.Uid + 2000, 300));
        }
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<string> Entries { get; } = [];

        public List<(LogLevel Level, string Message)> EntriesWithLevels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            Entries.Add(message);
            EntriesWithLevels.Add((logLevel, message));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeApiExecutionResultStore : IApiExecutionResultStore
    {
        public List<ApiExecutionResult> Results { get; } = [];

        public Task RecordAsync(ApiExecutionResult result, CancellationToken cancellationToken = default)
        {
            Results.Add(result);
            return Task.CompletedTask;
        }

        public List<(string ExecutionId, ReceivedMailId MailId)> MovedMailIds { get; } = [];

        public Task RecordMovedMailIdAsync(string executionId, ReceivedMailId movedMailId, CancellationToken cancellationToken = default)
        {
            MovedMailIds.Add((executionId, movedMailId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMoveFailureStore : IProcessedMailStore, IMailMoveFailureStore
    {
        public List<ReceivedMailId> MailIds { get; } = [];

        public HashSet<ReceivedMailId> ProcessedMailIds { get; } = [];

        Task<bool> IProcessedMailStore.ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessedMailIds.Contains(mailId));

        public Task RecordAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            _ = ProcessedMailIds.Add(mailId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MailMoveFailure> failures = [
                .. MailIds.Select(mailId => { return new MailMoveFailure(mailId, MailMoveFailureDestination.Processed); }),
                .. ErrorMoveFailureMailIds.Select(mailId => { return new MailMoveFailure(mailId, MailMoveFailureDestination.Error); })
            ];
            return Task.FromResult(failures);
        }

        public Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(MailIds.Contains(mailId) || ErrorMoveFailureMailIds.Contains(mailId));

        public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (!MailIds.Contains(mailId))
            {
                MailIds.Add(mailId);
            }

            return Task.CompletedTask;
        }

        public List<ReceivedMailId> ErrorMoveFailureMailIds { get; } = [];

        public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            if (!ErrorMoveFailureMailIds.Contains(mailId))
            {
                ErrorMoveFailureMailIds.Add(mailId);
            }

            return Task.CompletedTask;
        }

        public Task RecordRecoveryFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default)
        {
            _ = failure.Destination == MailMoveFailureDestination.Processed
                ? MailIds.Remove(failure.MailId)
                : ErrorMoveFailureMailIds.Remove(failure.MailId);

            return Task.CompletedTask;
        }

        public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            _ = MailIds.Remove(mailId);
            return Task.CompletedTask;
        }
    }
}
