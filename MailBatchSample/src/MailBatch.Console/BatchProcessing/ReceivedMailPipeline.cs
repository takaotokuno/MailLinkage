using System.Threading.Channels;
using MailBatch.Console.Models;
using MailBatch.Console.ReceivedMails;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class ReceivedMailPipeline(
    IReceivedMailQueueFactory queueFactory,
    IReceivedMailPipelineComponentFactory componentFactory,
    ILogger<ReceivedMailPipeline> logger) : IReceivedMailPipeline
{
    public async Task<ProcessResult> ProcessAsync(IReadOnlyList<UniqueId> targetUids, CancellationToken cancellationToken = default)
    {
        if (targetUids.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        Channel<ReceivedMailRequest> queue = queueFactory.Create();
        IMailFetchQueueProducer producer = componentFactory.CreateProducer(queue.Writer);
        IApiQueueConsumer consumer = componentFactory.CreateConsumer(queue.Reader);

        Task<ProcessResult> producerTask = producer.ProduceAsync(targetUids, cancellationToken);
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
            Total: targetUids.Count,
            Succeeded: consumerResult.Succeeded,
            Failed: producerResult.Failed + consumerResult.Failed);
    }
}
