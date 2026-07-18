using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.Pipeline;

public sealed class ReceivedMailPipelineTests
{
    [Fact]
    public async Task ProcessAsync_WhenConsumerFailsWhileBoundedQueueIsFull_CancelsBlockedProducerAndCompletes()
    {
        BoundedQueueFactory queueFactory = new(capacity: 1);
        InvalidOperationException consumerException = new("consumer failed");
        TestComponentFactory componentFactory = new(
            writer =>
            {
                return new BlockingProducer(writer);
            },
            _ =>
            {
                return new FailingConsumer(consumerException);
            });
        ReceivedMailPipeline pipeline = new(queueFactory, componentFactory, NullLogger<ReceivedMailPipeline>.Instance);
        ReceivedMailId[] targetMailIds = [new(1, 999), new(2, 999), new(3, 999)];

        Task<ProcessResult> processTask = pipeline.ProcessAsync(targetMailIds);
        Task completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(processTask, completedTask);
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            return processTask;
        });
        Assert.Same(consumerException, exception);
    }

    [Fact]
    public async Task ProcessAsync_WhenProducerFails_CancelsConsumerAndCompletes()
    {
        BoundedQueueFactory queueFactory = new(capacity: 1);
        InvalidOperationException producerException = new("producer failed");
        TestComponentFactory componentFactory = new(
            _ =>
            {
                return new FailingProducer(producerException);
            },
            reader =>
            {
                return new WaitingConsumer(reader);
            });
        ReceivedMailPipeline pipeline = new(queueFactory, componentFactory, NullLogger<ReceivedMailPipeline>.Instance);

        Task<ProcessResult> processTask = pipeline.ProcessAsync([new ReceivedMailId(1, 999)]);
        Task completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(processTask, completedTask);
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            return processTask;
        });
        Assert.Same(producerException, exception);
    }

    private sealed class BoundedQueueFactory(int capacity) : IReceivedMailQueueFactory
    {
        public Channel<MailLinkageRequest> Create()
        {
            return Channel.CreateBounded<MailLinkageRequest>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });
        }
    }

    private sealed class TestComponentFactory(
        Func<ChannelWriter<MailLinkageRequest>, IMailFetchQueueProducer> producerFactory,
        Func<ChannelReader<MailLinkageRequest>, IRequestQueueConsumer> consumerFactory) : IReceivedMailPipelineComponentFactory
    {
        public IMailFetchQueueProducer CreateProducer(ChannelWriter<MailLinkageRequest> writer) => producerFactory(writer);

        public IRequestQueueConsumer CreateConsumer(ChannelReader<MailLinkageRequest> reader) => consumerFactory(reader);
    }

    private sealed class BlockingProducer(ChannelWriter<MailLinkageRequest> writer) : IMailFetchQueueProducer
    {
        public async Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
        {
            foreach (ReceivedMailId mailId in targetMailIds)
            {
                await writer.WriteAsync(new MailLinkageRequest(mailId, $"key-{mailId.Uid}", "message"), cancellationToken);
            }

            _ = writer.TryComplete();
            return new ProcessResult(targetMailIds.Count, Succeeded: targetMailIds.Count);
        }
    }

    private sealed class FailingProducer(Exception exception) : IMailFetchQueueProducer
    {
        public Task<ProcessResult> ProduceAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
            => Task.FromException<ProcessResult>(exception);
    }

    private sealed class FailingConsumer(Exception exception) : IRequestQueueConsumer
    {
        public Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default)
            => Task.FromException<ProcessResult>(exception);
    }

    private sealed class WaitingConsumer(ChannelReader<MailLinkageRequest> reader) : IRequestQueueConsumer
    {
        public async Task<ProcessResult> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            await foreach (MailLinkageRequest _ in reader.ReadAllAsync(cancellationToken))
            {
            }

            return new ProcessResult(Total: 0);
        }
    }
}
