using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class BatchRunner(
    AppOptions options,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    IMailSearchService mailSearchService,
    IReceivedMailFolderService receivedMailFolderService,
    IReceivedMailPipeline receivedMailPipeline,
    IExitCodePolicy exitCodePolicy,
    IRunStatusNotifier runStatusNotifier)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync()
    {
        LogStart();

        ProcessResult result;

        try
        {
            await receivedMailFolderService.ConnectAsync();

            IReadOnlyList<UniqueId> targetUids = await mailSearchService.SearchTargetMessagesAsync();
            result = await receivedMailPipeline.ProcessAsync(targetUids);
        }
        finally
        {
            await receivedMailFolderService.DisconnectAsync();
        }

        int exitCode = exitCodePolicy.DetermineExitCode(result);

        await runStatusNotifier.NotifyAsync(result, exitCode);
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
}
