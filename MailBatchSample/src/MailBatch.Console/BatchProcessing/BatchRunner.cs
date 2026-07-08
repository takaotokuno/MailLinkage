using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Models;
using MailKit;
using MailBatch.Console.Infrastructure;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class BatchRunner(
    AppOptions options,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    ILoggerFactory loggerFactory,
    IReceivedMailApiClient receivedMailApiClient,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    IReceivedMailFolderService receivedMailFolderService)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LogStart();

        ProcessResult result;

        try
        {
            // メールサーバに接続し、必要なフォルダを準備する
            await receivedMailFolderService.ConnectAsync(cancellationToken);

            // 処理対象メールを取得する
            IReadOnlyList<UniqueId> targetUids = await SearchTargetMessagesAsync(cancellationToken);

            // メールを連携
            result = await ProcessMessagesAsync(targetUids, cancellationToken);
        }
        finally
        {
            await receivedMailFolderService.DisconnectAsync(CancellationToken.None);
        }

        // 終了処理
        int exitCode = ToExitCode(result);

        await mailNotifier.SendAsync(mailNotificationFactory.CreateRunStatusNotification(result, exitCode), cancellationToken);
        LogFinish(result);

        return exitCode;
    }

    /// <summary>
    /// バッチ開始時の実行IDと主要な設定値をログに出力します。
    /// </summary>
    private void LogStart()
    {
        logger.LogInformation("Mail batch started. RunId={RunId}", runContext.RunId);
        logger.LogInformation(
            "Configuration loaded. IMAP={ImapHost}:{ImapPort}, Mailbox={Mailbox}, ApiBaseUrl={ApiBaseUrl}, ApiEndpoint={ApiEndpoint}, LogDirectory={LogDirectory}",
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.Mailbox,
            options.Api.BaseUrl,
            options.Api.Endpoint,
            options.Batch.LogDirectory);
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    private async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(CancellationToken cancellationToken)
    {
        MailKit.Search.SearchQuery query = MailSearchQueryFactory.Create(options.MailSearch);
        return await receivedMailFolderService.SearchTargetMessagesAsync(query, options.MailSearch.MaxMessages, cancellationToken);
    }

    /// <summary>
    /// 対象メールを取得・加工するProducerと、API送信するConsumerを並行実行します。
    /// </summary>
    private async Task<ProcessResult> ProcessMessagesAsync(IReadOnlyList<UniqueId> targetUids, CancellationToken cancellationToken)
    {
        if (targetUids.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        // 非同期キューを作成する。
        // UnboundedChannel: 上限なしのキュー
        // SingleReader/SingleWriter を True に設定すると、読み手・書き手が1つである前提で.NETが処理を最適化する
        Channel<ReceivedMailRequest> queue = Channel.CreateUnbounded<ReceivedMailRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        MailFetchQueueProducer producer = new(
            receivedMailFolderService,
            queue.Writer,
            mailNotifier,
            mailNotificationFactory,
            loggerFactory.CreateLogger<MailFetchQueueProducer>());

        ApiQueueConsumer consumer = new(
            options,
            receivedMailFolderService,
            receivedMailApiClient,
            queue.Reader,
            loggerFactory.CreateLogger<ApiQueueConsumer>());

        // Producer と Consumer を並行して実行し完了まで待機する。
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

    /// <summary>
    /// バッチ終了時の処理結果をログに出力します。
    /// </summary>
    private void LogFinish(ProcessResult result)
    {
        logger.LogInformation(
            "Mail batch finished. Succeeded={Succeeded}, Failed={Failed}, Total={Total}",
            result.Succeeded,
            result.Failed,
            result.Total);
    }

    /// <summary>
    /// 処理結果からプロセス終了コードを決定します。
    /// </summary>
    private static int ToExitCode(ProcessResult result)
    {
        return result.Failed > 0 ? 2 : 0;
    }
}
