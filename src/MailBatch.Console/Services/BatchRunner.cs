using System.Net.Http.Json;
using MailBatch.Console.Mail;
using MailBatch.Console.Options;
using MailKit;
using MailKit.Net.Imap;
using Serilog;
using Serilog.Context;

namespace MailBatch.Console.Services;

internal sealed class BatchRunner(AppOptions options, string runId)
{
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

    private static ImapClient CreateImapClient()
    {
        var imapClient = new ImapClient();
        imapClient.ServerCertificateValidationCallback = (_, _, _, _) => true;
        return imapClient;
    }

    private async Task ConnectImapAsync(ImapClient imapClient)
    {
        Log.Information("Connecting to IMAP server. Host={Host}, Port={Port}, UseSsl={UseSsl}", options.Imap.Host, options.Imap.Port, options.Imap.UseSsl);
        await imapClient.ConnectAsync(options.Imap.Host, options.Imap.Port, ImapSecurity.ToSecureSocketOptions(options.Imap.UseSsl));
        await imapClient.AuthenticateAsync(options.Imap.UserName, options.Imap.Password);
        Log.Information("Connected and authenticated to IMAP server. Host={Host}, UserName={UserName}", options.Imap.Host, options.Imap.UserName);
    }

    private async Task<IMailFolder> OpenMailboxAsync(ImapClient imapClient)
    {
        var folder = await imapClient.GetFolderAsync(options.Imap.Mailbox);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        return folder;
    }

    private async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(IMailFolder folder)
    {
        var query = MailSearchQueryFactory.Create(options.MailSearch);
        var uids = await folder.SearchAsync(query);
        var targetUids = uids.Take(options.MailSearch.MaxMessages).ToList();
        Log.Information("Found {MessageCount} target messages. Mailbox={Mailbox}", targetUids.Count, options.Imap.Mailbox);
        return targetUids;
    }

    private HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            BaseAddress = options.Api.BaseUrl,
            Timeout = TimeSpan.FromSeconds(options.Api.TimeoutSeconds)
        };
    }

    private async Task<ProcessResult> ProcessMessagesAsync(IMailFolder folder, IReadOnlyList<UniqueId> targetUids, HttpClient httpClient)
    {
        var result = new ProcessResult(Total: targetUids.Count);

        foreach (var uid in targetUids)
        {
            result = result.Add(await ProcessMessageAsync(folder, uid, httpClient));
        }

        return result;
    }

    private async Task<bool> ProcessMessageAsync(IMailFolder folder, UniqueId uid, HttpClient httpClient)
    {
        var dto = await CreateRequestAsync(folder, uid);

        using (LogContext.PushProperty("MessageId", dto.MessageId))
        {
            return await PostAndHandleResultAsync(folder, uid, httpClient, dto);
        }
    }

    private static async Task<Models.ReceivedMailRequest> CreateRequestAsync(IMailFolder folder, UniqueId uid)
    {
        var message = await folder.GetMessageAsync(uid);
        var summary = await folder.FetchAsync(new[] { uid }, MessageSummaryItems.InternalDate);
        return ReceivedMailMapper.ToRequest(message, summary.FirstOrDefault()?.InternalDate);
    }

    private async Task<bool> PostAndHandleResultAsync(IMailFolder folder, UniqueId uid, HttpClient httpClient, Models.ReceivedMailRequest dto)
    {
        try
        {
            return await PostMessageAsync(folder, uid, httpClient, dto);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error while processing message. MessageId={MessageId}", dto.MessageId);
            return false;
        }
    }

    private async Task<bool> PostMessageAsync(IMailFolder folder, UniqueId uid, HttpClient httpClient, Models.ReceivedMailRequest dto)
    {
        Log.Information("Posting message to API. MessageId={MessageId}, Subject={Subject}", dto.MessageId, dto.Subject);
        using var response = await httpClient.PostAsJsonAsync(options.Api.Endpoint, dto);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LogApiFailure(dto.MessageId, (int)response.StatusCode, responseBody);
            return false;
        }

        await HandleSuccessfulPostAsync(folder, uid, dto.MessageId, (int)response.StatusCode, responseBody);
        return true;
    }

    private async Task HandleSuccessfulPostAsync(IMailFolder folder, UniqueId uid, string messageId, int statusCode, string responseBody)
    {
        LogApiSuccess(messageId, statusCode, responseBody);
        await MarkAsSeenIfNeededAsync(folder, uid, messageId);
    }

    private static void LogApiSuccess(string messageId, int statusCode, string responseBody)
    {
        Log.Information(
            "API post succeeded. MessageId={MessageId}, StatusCode={StatusCode}, SavedId={SavedId}",
            messageId,
            statusCode,
            ApiResponseSummary.ExtractSavedId(responseBody));
    }

    private static void LogApiFailure(string messageId, int statusCode, string responseBody)
    {
        Log.Warning(
            "API post failed. MessageId={MessageId}, StatusCode={StatusCode}, Response={ResponseSummary}",
            messageId,
            statusCode,
            ApiResponseSummary.Summarize(responseBody));
    }

    private async Task MarkAsSeenIfNeededAsync(IMailFolder folder, UniqueId uid, string messageId)
    {
        if (!options.Processing.MarkAsSeenOnSuccess)
        {
            return;
        }

        await folder.AddFlagsAsync(uid, MessageFlags.Seen, true);
        Log.Information("Marked message as seen. MessageId={MessageId}", messageId);
    }

    private static async Task DisconnectImapAsync(ImapClient imapClient)
    {
        await imapClient.DisconnectAsync(true);
    }

    private static void LogFinish(ProcessResult result)
    {
        Log.Information("Mail batch finished. Succeeded={Succeeded}, Failed={Failed}, Total={Total}", result.Succeeded, result.Failed, result.Total);
    }

    private static int ToExitCode(ProcessResult result)
    {
        return result.Failed > 0 ? 2 : 0;
    }

    private sealed record ProcessResult(int Total, int Succeeded = 0, int Failed = 0)
    {
        public ProcessResult Add(bool succeeded)
        {
            return succeeded ? this with { Succeeded = Succeeded + 1 } : this with { Failed = Failed + 1 };
        }
    }
}
