using MailBatch.Console.BatchProcessing.Locking;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.Searching;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// メール検索から処理結果通知までのバッチ全体を実行します。
/// </summary>
internal sealed class BatchRunner(
    ImapOptions imapOptions,
    ApiOptions apiOptions,
    BatchOptions batchOptions,
    MailSearchOptions mailSearchOptions,
    BatchRunContext runContext,
    ILogger<BatchRunner> logger,
    IReceivedMailPipeline receivedMailPipeline,
    IBatchRunCompletionService runCompletionService,
    IReceivedMailSession receivedMailSession,
    IMailMoveFailureRecoveryService mailMoveFailureRecoveryService,
    IJobExecutionLock jobExecutionLock)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        LogStart();

        using JobExecutionLockHandle? executionLock = jobExecutionLock.TryAcquire();
        if (executionLock is null)
        {
            return await HandleDuplicateRunAsync(startedAt, cancellationToken);
        }

        ProcessResult processResult = await ExecuteLockedRunAsync(startedAt, cancellationToken);
        BatchRunResult runResult = new(processResult, startedAt, DateTimeOffset.UtcNow);
        return await CompleteRunAsync(runResult, cancellationToken);
    }

    /// <summary>
    /// 実行ロック取得後の接続、復旧、メール処理を実行します。
    /// </summary>
    private async Task<ProcessResult> ExecuteLockedRunAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        string fatalErrorStage = "Connection";

        try
        {
            try
            {
                await receivedMailSession.ConnectAsync(cancellationToken);
                fatalErrorStage = "Processing";

                await mailMoveFailureRecoveryService.RecoverAsync(cancellationToken);

                return await RunUseCaseAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await NotifyFatalErrorAsync(ex, fatalErrorStage, startedAt);
                throw;
            }
        }
        finally
        {
            await receivedMailSession.DisconnectAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// 致命的なエラーを実行結果として通知し、ログに記録します。
    /// </summary>
    private async Task NotifyFatalErrorAsync(Exception exception, string stage, DateTimeOffset startedAt)
    {
        BatchRunResult fatalRunResult = CreateFatalRunResult(exception, stage, startedAt);
        int fatalExitCode = fatalRunResult.ConvertToExitCode();

        await runCompletionService.CompleteAsync(fatalRunResult, fatalExitCode, CancellationToken.None);
        logger.LogError(
            exception,
            "Mail batch failed with a fatal error before completion. RunId={RunId}, Stage={Stage}",
            runContext.RunId,
            stage);
    }

    /// <summary>
    /// 通常の実行結果を通知し、終了ログを出力したうえで終了コードを返します。
    /// </summary>
    private async Task<int> CompleteRunAsync(BatchRunResult runResult, CancellationToken cancellationToken)
    {
        int exitCode = runResult.ConvertToExitCode();

        await runCompletionService.CompleteAsync(runResult, exitCode, cancellationToken);
        LogFinish(runResult.ProcessResult);

        return exitCode;
    }

    /// <summary>
    /// 二重起動を検知したエラーを通知し、終了コードを返します。
    /// </summary>
    private async Task<int> HandleDuplicateRunAsync(
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        BatchRunResult result = new(
            new ProcessResult(Total: 0),
            startedAt,
            DateTimeOffset.UtcNow,
            new FatalBatchError(
                Code: "DuplicateRun",
                Message: "Another mail batch instance is already running.",
                Stage: "Startup"));

        int exitCode = result.ConvertToExitCode();

        await runCompletionService.CompleteAsync(
            result,
            exitCode,
            cancellationToken);

        logger.LogError(
            "Mail batch aborted because another instance is already running. RunId={RunId}",
            runContext.RunId);

        return exitCode;
    }

    /// <summary>
    /// 例外情報から致命的なバッチ実行結果を作成します。
    /// </summary>
    private static BatchRunResult CreateFatalRunResult(
        Exception exception,
        string stage,
        DateTimeOffset startedAt)
    {
        return new BatchRunResult(
            new ProcessResult(Total: 0),
            startedAt,
            DateTimeOffset.UtcNow,
            new FatalBatchError(
                Code: exception.GetType().Name,
                Message: exception.Message,
                Stage: stage));
    }

    /// <summary>
    /// 検索条件に一致するメールを取得し、受信メールパイプラインで処理します。
    /// </summary>
    private async Task<ProcessResult> RunUseCaseAsync(CancellationToken cancellationToken)
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(mailSearchOptions, DateTime.UtcNow);
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
