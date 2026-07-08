using System.Threading.Channels;
using MailBatch.Console.Mail;
using MailBatch.Console.Notifications;
using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Services;

internal sealed class BatchRunner(
    AppOptions options,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    ILoggerFactory loggerFactory,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync()
    {
        LogStart();

        // メールサーバに接続する
        using ImapClient imapClient = new();
        await ConnectImapAsync(imapClient);
        IMailFolder folder = await OpenMailboxAsync(imapClient);

        // 処理対象メールを取得する
        IReadOnlyList<UniqueId> targetUids = await SearchTargetMessagesAsync(folder);

        using HttpClient httpClient = CreateHttpClient();
        ProcessResult result = await ProcessMessagesAsync(folder, targetUids, httpClient);

        await DisconnectImapAsync(imapClient);

        LogFinish(result);

        int exitCode = ToExitCode(result);
        await mailNotifier.SendAsync(mailNotificationFactory.CreateRunStatusNotification(result, exitCode));

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
    /// 設定値を使用してIMAPサーバーに接続し、認証します。
    /// </summary>
    private async Task ConnectImapAsync(ImapClient imapClient)
    {
        logger.LogInformation(
            "Connecting to IMAP server. Host={Host}, Port={Port}, SecureSocketOption={SecureSocketOption}",
            options.Imap.Host, options.Imap.Port,
            options.Imap.SecureSocketOption);

        await imapClient.ConnectAsync(
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.GetSecureSocketOptions());
        await imapClient.AuthenticateAsync(options.Imap.UserName, options.Imap.Password);

        logger.LogInformation(
            "Connected and authenticated to IMAP server. Host={Host}, UserName={UserName}",
            options.Imap.Host,
            options.Imap.UserName);
    }

    /// <summary>
    /// 設定されたメールボックスを読み書き可能な状態で開きます。
    /// </summary>
    private async Task<IMailFolder> OpenMailboxAsync(ImapClient imapClient)
    {
        IMailFolder folder = await imapClient.GetFolderAsync(options.Imap.Mailbox);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        return folder;
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    private async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(IMailFolder folder)
    {
        MailKit.Search.SearchQuery query = MailSearchQueryFactory.Create(options.MailSearch);
        IList<UniqueId> uids = await folder.SearchAsync(query);
        List<UniqueId> targetUids = uids.Take(options.MailSearch.MaxMessages).ToList();

        logger.LogInformation(
            "Found {MessageCount} target messages. Mailbox={Mailbox}",
            targetUids.Count,
            options.Imap.Mailbox);

        return targetUids;
    }

    /// <summary>
    /// API送信用のベースURLとタイムアウトを設定したHTTPクライアントを作成します。
    /// </summary>
    private HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = options.Api.BaseUrl,
            Timeout = TimeSpan.FromSeconds(options.Api.TimeoutSeconds)
        };
    }

    /// <summary>
    /// 対象メールを取得・加工するProducerと、API送信するConsumerを並行実行します。
    /// </summary>
    private async Task<ProcessResult> ProcessMessagesAsync(
        IMailFolder folder,
        IReadOnlyList<UniqueId> targetUids,
        HttpClient httpClient)
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

        // IMAPフォルダ操作を同時に実行しないためロック
        // Producerはメール取得、Consumerはメール移動でIMAPフォルダを触るため、同じLockを共有する。
        using SemaphoreSlim imapLock = new(1, 1);

        MailFetchQueueProducer producer = new(
            folder,
            queue.Writer,
            imapLock,
            mailNotifier,
            mailNotificationFactory,
            loggerFactory.CreateLogger<MailFetchQueueProducer>());

        ApiQueueConsumer consumer = new(
            options,
            folder,
            httpClient,
            queue.Reader,
            imapLock,
            loggerFactory.CreateLogger<ApiQueueConsumer>());

        // Producer と Consumer を並行して実行し完了まで待機する。
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

    /// <summary>
    /// IMAPサーバーから正常に切断します。
    /// </summary>
    private async Task DisconnectImapAsync(ImapClient imapClient)
    {
        await imapClient.DisconnectAsync(true);
        logger.LogInformation("Disconnecting from IMAP server.");
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
