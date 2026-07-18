using System.Threading.Channels;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.ReceivedMails;
using Microsoft.Extensions.Logging;

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

        Channel<MailLinkageRequest> queue = queueFactory.Create();
        IMailFetchQueueProducer producer = componentFactory.CreateProducer(queue.Writer);
        IRequestQueueConsumer consumer = componentFactory.CreateConsumer(queue.Reader);

        Task<ProcessResult> producerTask = producer.ProduceAsync(targetMailIds, cancellationToken);
        Task<ProcessResult> consumerTask = consumer.ConsumeAsync(cancellationToken);

        ProcessResult producerResult = await producerTask;
        ProcessResult consumerResult = await consumerTask;

        logger.LogInformation(
            "Queue processing completed. Enqueued={Enqueued}, InvalidFormat={InvalidFormat}, ApiSucceeded={ApiSucceeded}, ApiFailed={ApiFailed}",
            producerResult.Succeeded,
            producerResult.InvalidFormat,
            consumerResult.Succeeded,
            consumerResult.ApiFailed);

        return new ProcessResult(
            Total: targetMailIds.Count,
            Succeeded: consumerResult.Succeeded,
            InvalidFormat: producerResult.InvalidFormat,
            ApiFailed: consumerResult.ApiFailed);
    }
}
