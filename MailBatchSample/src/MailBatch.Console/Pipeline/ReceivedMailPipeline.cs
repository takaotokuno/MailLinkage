using System.Threading.Channels;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.ReceivedMails;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

/// <summary>
/// 対象メールの読取からAPI連携までの一連の処理を実行します。
/// </summary>
internal interface IReceivedMailPipeline
{
    /// <summary>
    /// 指定された受信メールID一覧をProducer/Consumerパイプラインで処理します。
    /// </summary>
    Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// 受信メール処理をキュー経由で並行実行します。
/// </summary>
internal sealed class ReceivedMailPipeline(
    IReceivedMailQueueFactory queueFactory,
    IReceivedMailPipelineComponentFactory componentFactory,
    ILogger<ReceivedMailPipeline> logger) : IReceivedMailPipeline
{
    /// <summary>
    /// 受信メールID一覧をプロデューサーとコンシューマーで処理します。
    /// </summary>
    public async Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default)
    {
        if (targetMailIds.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        Channel<MailLinkageRequest> queue = queueFactory.Create();
        IMailFetchQueueProducer producer = componentFactory.CreateProducer(queue.Writer);
        IRequestQueueConsumer consumer = componentFactory.CreateConsumer(queue.Reader);

        // Producer/Consumerのどちらかで致命的な失敗が起きた場合に片側だけが待ち続けないよう、
        // 共有トークンでパイプライン全体を停止できるようにします。
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

    /// <summary>
    /// パイプライン内の先行失敗を検知して他方の処理を停止します。
    /// </summary>
    private static async Task CancelPipelineOnFirstFailureAsync(
        Task<ProcessResult> producerTask,
        Task<ProcessResult> consumerTask,
        ChannelWriter<MailLinkageRequest> writer,
        CancellationTokenSource cancellationTokenSource)
    {
        // 先に失敗したタスクを検知してキューを閉じることで、未処理データの増加やConsumerの永久待機を防ぎます。
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
