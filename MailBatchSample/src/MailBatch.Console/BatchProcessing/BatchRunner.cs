using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Searching;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class BatchRunner(
    ImapOptions imapOptions,
    ApiOptions apiOptions,
    BatchOptions batchOptions,
    MailSearchOptions mailSearchOptions,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    IReceivedMailPipeline receivedMailPipeline,
    IRunStatusNotifier runStatusNotifier,
    IReceivedMailSession receivedMailSession)
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
            await receivedMailSession.ConnectAsync(cancellationToken);
            result = await RunUseCaseAsync(cancellationToken);
        }
        finally
        {
            await receivedMailSession.DisconnectAsync(CancellationToken.None);
        }

        int exitCode = result.ConvertToExitCode();

        await runStatusNotifier.NotifyAsync(result, exitCode, cancellationToken);
        LogFinish(result);

        return exitCode;
    }

    private async Task<ProcessResult> RunUseCaseAsync(CancellationToken cancellationToken)
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(mailSearchOptions, DateTime.Now);
        IReadOnlyList<ReceivedMailId> targetMailIds = await receivedMailSession.SearchTargetMessagesAsync(
            condition,
            mailSearchOptions.MaxMessages,
            cancellationToken);
        return await receivedMailPipeline.ProcessAsync(targetMailIds, cancellationToken);
    }

    /// <summary>
    /// バッチ開始時の実行IDと主要な設定値をログに出力します。
    /// </summary>
    private void LogStart()
    {
        logger.LogInformation("Mail batch started. RunId={RunId}", runContext.RunId);
        logger.LogInformation(
            "Configuration loaded. IMAP={ImapHost}:{ImapPort}, Mailbox={Mailbox}, ApiBaseUrl={ApiBaseUrl}, ApiEndpoint={ApiEndpoint}, LogDirectory={LogDirectory}, LogRetentionDays={LogRetentionDays}",
            imapOptions.Host,
            imapOptions.Port,
            imapOptions.Mailbox,
            apiOptions.BaseUrl,
            apiOptions.Endpoint,
            batchOptions.LogDirectory,
            batchOptions.LogRetentionDays);
    }

    /// <summary>
    /// バッチ終了時の処理結果をログに出力します。
    /// </summary>
    private void LogFinish(ProcessResult result)
    {
        logger.LogInformation(
            "Mail batch finished. MailCount={MailCount}, Succeeded={Succeeded}, InvalidFormat={InvalidFormat}, ApiFailed={ApiFailed}",
            result.Total,
            result.Succeeded,
            result.InvalidFormat,
            result.ApiFailed);
    }
}
