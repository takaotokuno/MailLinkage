using System.Threading.Channels;
using MailBatch.Console.Models;
using MailBatch.Console.ReceivedMails;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal delegate IMailFetchQueueProducer MailFetchQueueProducerFactory(ChannelWriter<ReceivedMailRequest> writer);

internal delegate IApiQueueConsumer ApiQueueConsumerFactory(ChannelReader<ReceivedMailRequest> reader);

/// <summary>
/// 受信メール連携のProducer / Consumerパイプラインを構成して実行します。
/// </summary>
internal sealed class ReceivedMailPipeline(
    IQueueFactory<ReceivedMailRequest> queueFactory,
    MailFetchQueueProducerFactory producerFactory,
    ApiQueueConsumerFactory consumerFactory,
    ILogger<ReceivedMailPipeline> logger) : IReceivedMailPipeline
{
    public async Task<ProcessResult> ProcessAsync(IReadOnlyList<UniqueId> targetUids)
    {
        if (targetUids.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        Channel<ReceivedMailRequest> queue = queueFactory.CreateSingleReaderSingleWriterQueue();
        IMailFetchQueueProducer producer = producerFactory(queue.Writer);
        IApiQueueConsumer consumer = consumerFactory(queue.Reader);

        Task<ProcessResult> producerTask = producer.ProduceAsync(targetUids);
        Task<ProcessResult> consumerTask = consumer.ConsumeAsync();

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
