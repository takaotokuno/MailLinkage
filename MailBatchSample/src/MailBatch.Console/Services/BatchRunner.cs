using System.Threading.Channels;
using MailBatch.Console.Mail;
using MailBatch.Console.Options;
using MailKit;
using MailKit.Net.Imap;
using Serilog;

namespace MailBatch.Console.Services;

internal sealed class BatchRunner(AppOptions options, string runId)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync()
    {
        LogStart();
        using var imapClient = CreateImapClient();
        await ConnectImapAsync(imapClient);
        var folder = await OpenMailboxAsync(imapClient);
        var targetUids = await SearchTargetMessagesAsync(folder);
        using var httpClient = CreateHttpClient();
        var result = await ProcessMessagesAsync(folder, targetUids, httpClient);
        await DisconnectImapAsync(imapClient);
        LogFinish(result);
        return ToExitCode(result);
    }

    /// <summary>
    /// バッチ開始時の実行IDと主要な設定値をログに出力します。
    /// </summary>
    private void LogStart()
    {
        Log.Information("Mail batch started. RunId={RunId}", runId);
        Log.Information(
            "Configuration loaded. IMAP={ImapHost}:{ImapPort}, Mailbox={Mailbox}, ApiBaseUrl={ApiBaseUrl}, ApiEndpoint={ApiEndpoint}, LogDirectory={LogDirectory}",
            options.Imap.Host,
            options.Imap.Port,
            options.Imap.Mailbox,
            options.Api.BaseUrl,
            options.Api.Endpoint,
            options.Batch.LogDirectory);
    }

    /// <summary>
    /// サーバー証明書の検証コールバックを設定したIMAPクライアントを作成します。
    /// </summary>
    private static ImapClient CreateImapClient()
    {
        var imapClient = new ImapClient();
        imapClient.ServerCertificateValidationCallback = (_, _, _, _) => true;
        return imapClient;
    }

    /// <summary>
    /// 設定値を使用してIMAPサーバーに接続し、認証します。
    /// </summary>
    private async Task ConnectImapAsync(ImapClient imapClient)
    {
        Log.Information("Connecting to IMAP server. Host={Host}, Port={Port}, UseSsl={UseSsl}", options.Imap.Host, options.Imap.Port, options.Imap.UseSsl);
        await imapClient.ConnectAsync(options.Imap.Host, options.Imap.Port, ImapSecurity.ToSecureSocketOptions(options.Imap.UseSsl));
        await imapClient.AuthenticateAsync(options.Imap.UserName, options.Imap.Password);
        Log.Information("Connected and authenticated to IMAP server. Host={Host}, UserName={UserName}", options.Imap.Host, options.Imap.UserName);
    }

    /// <summary>
    /// 設定されたメールボックスを読み書き可能な状態で開きます。
    /// </summary>
    private async Task<IMailFolder> OpenMailboxAsync(ImapClient imapClient)
    {
        var folder = await imapClient.GetFolderAsync(options.Imap.Mailbox);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        return folder;
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    private async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(IMailFolder folder)
    {
        var query = MailSearchQueryFactory.Create(options.MailSearch);
        var uids = await folder.SearchAsync(query);
        var targetUids = uids.Take(options.MailSearch.MaxMessages).ToList();
        Log.Information("Found {MessageCount} target messages. Mailbox={Mailbox}", targetUids.Count, options.Imap.Mailbox);
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
    private async Task<ProcessResult> ProcessMessagesAsync(IMailFolder folder, IReadOnlyList<UniqueId> targetUids, HttpClient httpClient)
    {
        if (targetUids.Count == 0)
        {
            return new ProcessResult(Total: 0);
        }

        var queue = Channel.CreateUnbounded<ApiQueueItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
        using var imapLock = new SemaphoreSlim(1, 1);

        var producer = new MailFetchQueueProducer(folder, queue.Writer, imapLock);
        var consumer = new ApiQueueConsumer(options, folder, httpClient, queue.Reader, imapLock);

        var producerTask = producer.ProduceAsync(targetUids);
        var consumerTask = consumer.ConsumeAsync();

        var producerResult = await producerTask;
        var consumerResult = await consumerTask;

        Log.Information(
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
    private static async Task DisconnectImapAsync(ImapClient imapClient)
    {
        await imapClient.DisconnectAsync(true);
    }

    /// <summary>
    /// バッチ終了時の処理結果をログに出力します。
    /// </summary>
    private static void LogFinish(ProcessResult result)
    {
        Log.Information("Mail batch finished. Succeeded={Succeeded}, Failed={Failed}, Total={Total}", result.Succeeded, result.Failed, result.Total);
    }

    /// <summary>
    /// 処理結果からプロセス終了コードを決定します。
    /// </summary>
    private static int ToExitCode(ProcessResult result)
    {
        return result.Failed > 0 ? 2 : 0;
    }
}
