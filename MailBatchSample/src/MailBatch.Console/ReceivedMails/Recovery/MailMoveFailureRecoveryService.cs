using MailBatch.Console.NotificationMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Recovery;

/// <summary>
/// 前回実行で失敗したメール移動を復旧します。
/// </summary>
internal interface IMailMoveFailureRecoveryService
{
    /// <summary>
    /// 記録済みのメール移動失敗を再試行します。
    /// </summary>
    Task RecoverAsync(CancellationToken cancellationToken);
}

/// <summary>
/// メール移動失敗ストアの記録をもとにメール移動を再試行します。
/// </summary>
internal sealed class MailMoveFailureRecoveryService(
    IReceivedMailMover receivedMailMover,
    IMailMoveFailureStore moveFailureStore,
    IStateMetricAlertMonitor metricAlertMonitor,
    ILogger<MailMoveFailureRecoveryService> logger) : IMailMoveFailureRecoveryService
{
    /// <inheritdoc />
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<MailMoveFailure> failures = await moveFailureStore.GetAllAsync(cancellationToken);
        List<MailMoveFailure> unrecoveredFailures = [];
        foreach (MailMoveFailure failure in failures)
        {
            if (!await RecoverMailboxMoveFailureAsync(failure, cancellationToken))
            {
                unrecoveredFailures.Add(failure);
            }
        }

        _ = await metricAlertMonitor.TryCheckMailMoveStagnationAsync(unrecoveredFailures, cancellationToken);
    }

    /// <summary>
    /// メールボックス移動失敗レコード1件に対する再移動と記録削除を実行します。
    /// </summary>
    private async Task<bool> RecoverMailboxMoveFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken)
    {
        try
        {
            await MoveRecoveredMailAsync(failure, cancellationToken);
            await moveFailureStore.RemoveAsync(failure, cancellationToken);
            logger.LogDebug(
                "Recovered mailbox move failure. MailId={MailId}, Destination={Destination}",
                failure.MailId,
                failure.Destination);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await moveFailureStore.RecordRecoveryFailureAsync(failure, cancellationToken);
            logger.LogError(
                ex,
                "Failed to recover mailbox move failure. MailId={MailId}, Destination={Destination}",
                failure.MailId,
                failure.Destination);
            return false;
        }
    }

    /// <summary>
    /// 失敗レコードの移動先に応じて受信メールを移動します。
    /// </summary>
    private async Task MoveRecoveredMailAsync(MailMoveFailure failure, CancellationToken cancellationToken)
    {
        if (failure.Destination == MailMoveFailureDestination.Processed)
        {
            _ = await receivedMailMover.MoveToProcessedMailboxAsync(failure.MailId, cancellationToken);
            return;
        }

        _ = await receivedMailMover.MoveToErrorMailboxAsync(failure.MailId, cancellationToken);
    }
}
