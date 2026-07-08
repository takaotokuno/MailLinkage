using MailBatch.Console.Models;
using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class BatchRunner(
    AppOptions options,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    IMailSearchService mailSearchService,
    IReceivedMailPipeline receivedMailPipeline,
    IExitCodePolicy exitCodePolicy,
    IRunStatusNotifier runStatusNotifier,
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
            await receivedMailFolderService.ConnectAsync(cancellationToken);
            result = await RunUseCaseAsync(cancellationToken);
        }
        finally
        {
            await receivedMailFolderService.DisconnectAsync(CancellationToken.None);
        }

        int exitCode = exitCodePolicy.GetExitCode(result);

        await runStatusNotifier.NotifyAsync(result, exitCode, cancellationToken);
        LogFinish(result);

        return exitCode;
    }

    private async Task<ProcessResult> RunUseCaseAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MailKit.UniqueId> targetUids = await mailSearchService.SearchTargetMessagesAsync(cancellationToken);
        return await receivedMailPipeline.ProcessAsync(targetUids, cancellationToken);
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
