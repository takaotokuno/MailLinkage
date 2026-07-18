using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Searching;
using MailBatch.Console.ReceivedMails.State;
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
    IReceivedMailSession receivedMailSession,
    IProcessedMailMoveFailureStore moveFailureStore,
    IJobExecutionLock jobExecutionLock)
{
    /// <summary>
    /// メール取得からAPI送信までのバッチ処理全体を実行し、終了コードを返します。
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        LogStart();

        using JobExecutionLockHandle? executionLock = jobExecutionLock.TryAcquire();
        if (executionLock is null)
        {
            BatchRunResult duplicateRunResult = new(
                new ProcessResult(Total: 0),
                new FatalBatchError(
                    Code: "DuplicateRun",
                    Message: "Another mail batch instance is already running.",
                    Stage: "Startup"));
            int duplicateRunExitCode = duplicateRunResult.ConvertToExitCode();

            _ = await runStatusNotifier.TryNotifyAsync(duplicateRunResult, duplicateRunExitCode, cancellationToken);
            logger.LogError(
                "Mail batch aborted because another instance is already running. RunId={RunId}",
                runContext.RunId);

            return duplicateRunExitCode;
        }

        BatchRunResult runResult;
        string fatalErrorStage = "Connection";

        try
        {
            try
            {
                await receivedMailSession.ConnectAsync(cancellationToken);
                fatalErrorStage = "Processing";

                await RecoverMailboxMoveFailuresAsync(cancellationToken);

                ProcessResult processResult = await RunUseCaseAsync(cancellationToken);
                runResult = new BatchRunResult(processResult);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                BatchRunResult fatalRunResult = CreateFatalRunResult(ex, fatalErrorStage);
                int fatalExitCode = fatalRunResult.ConvertToExitCode();

                _ = await runStatusNotifier.TryNotifyAsync(fatalRunResult, fatalExitCode, CancellationToken.None);
                logger.LogError(
                    ex,
                    "Mail batch failed with a fatal error before completion. RunId={RunId}, Stage={Stage}",
                    runContext.RunId,
                    fatalErrorStage);

                throw;
            }
        }
        finally
        {
            await receivedMailSession.DisconnectAsync(CancellationToken.None);
        }

        int exitCode = runResult.ConvertToExitCode();

        _ = await runStatusNotifier.TryNotifyAsync(runResult, exitCode, cancellationToken);
        LogFinish(runResult.ProcessResult);

        return exitCode;
    }

    private static BatchRunResult CreateFatalRunResult(Exception exception, string stage)
    {
        return new BatchRunResult(
            new ProcessResult(Total: 0),
            new FatalBatchError(
                Code: exception.GetType().Name,
                Message: exception.Message,
                Stage: stage));
    }

    private async Task RecoverMailboxMoveFailuresAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MailMoveFailure> failures = await moveFailureStore.GetAllAsync(cancellationToken);
        foreach (MailMoveFailure failure in failures)
        {
            try
            {
                if (failure.Destination == MailMoveFailureDestination.Processed)
                {
                    await receivedMailSession.MoveToProcessedMailboxAsync(failure.MailId, cancellationToken);
                }
                else
                {
                    await receivedMailSession.MoveToErrorMailboxAsync(failure.MailId, cancellationToken);
                }

                await moveFailureStore.RemoveAsync(failure, cancellationToken);
                logger.LogInformation(
                    "Recovered mailbox move failure. MailId={MailId}, Destination={Destination}",
                    failure.MailId,
                    failure.Destination);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to recover mailbox move failure. MailId={MailId}, Destination={Destination}",
                    failure.MailId,
                    failure.Destination);
            }
        }
    }

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
