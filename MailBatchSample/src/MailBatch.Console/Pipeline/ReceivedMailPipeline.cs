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

        using CancellationTokenSource linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken pipelineCancellationToken = linkedCancellationTokenSource.Token;

        Task<ProcessResult> producerTask = producer.ProduceAsync(targetMailIds, pipelineCancellationToken);
        Task<ProcessResult> consumerTask = consumer.ConsumeAsync(pipelineCancellationToken);

        await CancelPipelineOnFirstFailureAsync(producerTask, consumerTask, queue.Writer, linkedCancellationTokenSource);

        ProcessResult producerResult = await producerTask;
        ProcessResult consumerResult = await consumerTask;

        logger.LogInformation(
            "Queue processing completed. Enqueued={Enqueued}, InvalidFormat={InvalidFormat}, ApiSucceeded={ApiSucceeded}, ApiFailed={ApiFailed}, MoveRecoveryFailed={MoveRecoveryFailed}",
            producerResult.Succeeded,
            producerResult.InvalidFormat,
            consumerResult.Succeeded,
            consumerResult.ApiFailed,
            producerResult.ApiFailed);

        return new ProcessResult(
            Total: targetMailIds.Count,
            Succeeded: consumerResult.Succeeded,
            InvalidFormat: producerResult.InvalidFormat,
            ApiFailed: consumerResult.ApiFailed + producerResult.ApiFailed);
    }

    private static async Task CancelPipelineOnFirstFailureAsync(
        Task<ProcessResult> producerTask,
        Task<ProcessResult> consumerTask,
        ChannelWriter<MailLinkageRequest> writer,
        CancellationTokenSource cancellationTokenSource)
    {
        Task<ProcessResult[]> allTasks = Task.WhenAll(producerTask, consumerTask);
        Task firstCompletedTask = await Task.WhenAny(allTasks, producerTask, consumerTask);

        if (firstCompletedTask.IsFaulted)
        {
            _ = writer.TryComplete(firstCompletedTask.Exception);
            await cancellationTokenSource.CancelAsync();

            await ((Task)allTasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await firstCompletedTask;
        }
        else if (firstCompletedTask.IsCanceled)
        {
            await cancellationTokenSource.CancelAsync();
        }

        _ = await allTasks;
    }
}
