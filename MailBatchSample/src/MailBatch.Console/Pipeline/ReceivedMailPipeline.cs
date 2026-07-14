using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Service;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace MailBatch.Console.Pipeline;

internal interface IReceivedMailPipeline
{
    Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}

internal sealed class ReceivedMailPipeline(
    IReceivedMailQueueFactory queueFactory,
    IReceivedMailPipelineComponentFactory componentFactory,
    ILogger<ReceivedMailPipeline> logger) : IReceivedMailPipeline
{
    public async Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
    {
        if (targetMailIds.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        Channel<ApiRequest> queue = queueFactory.Create();
        IMailFetchQueueProducer producer = componentFactory.CreateProducer(queue.Writer);
        IApiQueueConsumer consumer = componentFactory.CreateConsumer(queue.Reader);

        Task<ProcessResult> producerTask = producer.ProduceAsync(targetMailIds, cancellationToken);
        Task<ProcessResult> consumerTask = consumer.ConsumeAsync(cancellationToken);

        ProcessResult producerResult = await producerTask;
        ProcessResult consumerResult = await consumerTask;

        logger.LogInformation(
            "Queue processing completed. Enqueued={Enqueued}, QueueFailures={QueueFailures}, ApiSucceeded={ApiSucceeded}, ApiFailed={ApiFailed}",
            producerResult.Succeeded,
            producerResult.Failed,
            consumerResult.Succeeded,
            consumerResult.Failed);

        return new ProcessResult(
            Total: targetMailIds.Count,
            Succeeded: consumerResult.Succeeded,
            Failed: producerResult.Failed + consumerResult.Failed);
    }
}
