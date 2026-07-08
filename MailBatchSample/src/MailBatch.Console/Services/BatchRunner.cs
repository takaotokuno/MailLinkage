using System.Net.Http.Json;
using MailBatch.Console.Mail;
using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailKit;
using MailKit.Net.Imap;
using Serilog;
using Serilog.Context;

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
        var targetMailIds = await SearchTargetMessagesAsync(folder);
        using var httpClient = CreateHttpClient();
        var result = await ProcessMessagesAsync(folder, targetMailIds, httpClient);
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
    /// 検索条件に一致する処理対象メールの受信メールID一覧を取得します。
    /// </summary>
    private async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(IMailFolder folder)
    {
        var query = MailSearchQueryFactory.Create(options.MailSearch);
        var uids = await folder.SearchAsync(query);
        var targetMailIds = uids
            .Take(options.MailSearch.MaxMessages)
            .Select(ReceivedMailIdMapper.ToReceivedMailId)
            .ToList();
        Log.Information("Found {MessageCount} target messages. Mailbox={Mailbox}", targetMailIds.Count, options.Imap.Mailbox);
        return targetMailIds;
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
    /// 対象メールを順番に処理し、成功件数と失敗件数を集計します。
    /// </summary>
    private async Task<ProcessResult> ProcessMessagesAsync(IMailFolder folder, IReadOnlyList<ReceivedMailId> targetMailIds, HttpClient httpClient)
    {
        var result = new ProcessResult(Total: targetMailIds.Count);

        foreach (var mailId in targetMailIds)
        {
            result = result.Add(await ProcessMessageAsync(folder, mailId, httpClient));
        }

        return result;
    }

    /// <summary>
    /// 指定されたメールをAPI送信用リクエストに変換し、送信結果を返します。
    /// </summary>
    private async Task<bool> ProcessMessageAsync(IMailFolder folder, ReceivedMailId mailId, HttpClient httpClient)
    {
        var dto = await CreateRequestAsync(folder, mailId);

        using (LogContext.PushProperty("MessageId", dto.MessageId))
        {
            return await PostAndHandleResultAsync(folder, mailId, httpClient, dto);
        }
    }

    /// <summary>
    /// 指定された受信メールIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    private static async Task<ReceivedMailRequest> CreateRequestAsync(IMailFolder folder, ReceivedMailId mailId)
    {
        var uid = ReceivedMailIdMapper.ToUniqueId(mailId);
        var message = await folder.GetMessageAsync(uid);
        var summary = await folder.FetchAsync(new[] { uid }, MessageSummaryItems.InternalDate);
        return ReceivedMailMapper.ToRequest(message, summary.FirstOrDefault()?.InternalDate);
    }

    /// <summary>
    /// メール送信処理を実行し、予期しない例外をログに記録して失敗として扱います。
    /// </summary>
    private async Task<bool> PostAndHandleResultAsync(IMailFolder folder, ReceivedMailId mailId, HttpClient httpClient, ReceivedMailRequest dto)
    {
        try
        {
            return await PostMessageAsync(folder, mailId, httpClient, dto);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error while processing message. MessageId={MessageId}", dto.MessageId);
            return false;
        }
    }

    /// <summary>
    /// 受信メールリクエストをAPIへ送信し、レスポンスに応じた後続処理を行います。
    /// </summary>
    private async Task<bool> PostMessageAsync(IMailFolder folder, ReceivedMailId mailId, HttpClient httpClient, ReceivedMailRequest dto)
    {
        Log.Information("Posting message to API. MessageId={MessageId}, Subject={Subject}", dto.MessageId, dto.Subject);
        using var response = await httpClient.PostAsJsonAsync(options.Api.Endpoint, dto);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LogApiFailure(dto.MessageId, (int)response.StatusCode, responseBody);
            return false;
        }

        await HandleSuccessfulPostAsync(folder, mailId, dto.MessageId, (int)response.StatusCode, responseBody);
        return true;
    }

    /// <summary>
    /// API送信成功時のログ出力と既読化処理を行います。
    /// </summary>
    private async Task HandleSuccessfulPostAsync(IMailFolder folder, ReceivedMailId mailId, string messageId, int statusCode, string responseBody)
    {
        LogApiSuccess(messageId, statusCode, responseBody);
        await MarkAsSeenIfNeededAsync(folder, mailId, messageId);
    }

    /// <summary>
    /// API送信成功時のステータスコードと保存済みIDをログに出力します。
    /// </summary>
    private static void LogApiSuccess(string messageId, int statusCode, string responseBody)
    {
        Log.Information(
            "API post succeeded. MessageId={MessageId}, StatusCode={StatusCode}, SavedId={SavedId}",
            messageId,
            statusCode,
            ApiResponseSummary.ExtractSavedId(responseBody));
    }

    /// <summary>
    /// API送信失敗時のステータスコードとレスポンス概要をログに出力します。
    /// </summary>
    private static void LogApiFailure(string messageId, int statusCode, string responseBody)
    {
        Log.Warning(
            "API post failed. MessageId={MessageId}, StatusCode={StatusCode}, Response={ResponseSummary}",
            messageId,
            statusCode,
            ApiResponseSummary.Summarize(responseBody));
    }

    /// <summary>
    /// 設定で有効な場合、処理済みメールに既読フラグを付与します。
    /// </summary>
    private async Task MarkAsSeenIfNeededAsync(IMailFolder folder, ReceivedMailId mailId, string messageId)
    {
        if (!options.Processing.MarkAsSeenOnSuccess)
        {
            return;
        }

        await folder.AddFlagsAsync(ReceivedMailIdMapper.ToUniqueId(mailId), MessageFlags.Seen, true);
        Log.Information("Marked message as seen. MessageId={MessageId}", messageId);
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

    private sealed record ProcessResult(int Total, int Succeeded = 0, int Failed = 0)
    {
        /// <summary>
        /// 1件分の処理結果を集計に加算した新しい処理結果を返します。
        /// </summary>
        public ProcessResult Add(bool succeeded)
        {
            return succeeded ? this with { Succeeded = Succeeded + 1 } : this with { Failed = Failed + 1 };
        }
    }
}
