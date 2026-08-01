using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

/// <summary>バッチ実行終了後の履歴保存、通知、保持期間整理を完了させます。</summary>
internal interface IBatchRunCompletionService
{
    /// <summary>実行結果を保存し、必要な通知と正常完了後の保持期間整理を実行します。</summary>
    Task CompleteAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// 実行履歴の保存、履歴アラートの評価、実行結果通知、保持期間整理を順に実行します。
/// </summary>
internal sealed class BatchRunCompletionService(
    BatchRunContext runContext,
    IBatchRunHistoryStore historyStore,
    IHistoricalMetricAlertMonitor historicalMetricAlertMonitor,
    IRunStatusNotifier runStatusNotifier,
    IBatchDataRetentionService dataRetentionService,
    ILogger<BatchRunCompletionService> logger) : IBatchRunCompletionService
{
    /// <inheritdoc />
    public async Task CompleteAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default)
    {
        bool historySaved = false;
        try
        {
            await historyStore.AddAsync(BatchRunHistory.From(runContext.RunId, result, exitCode), cancellationToken);
            historySaved = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save batch run history. RunId={RunId}", runContext.RunId);
        }

        if (historySaved)
        {
            try
            {
                _ = await historicalMetricAlertMonitor.TryCheckAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate historical metric alerts. RunId={RunId}", runContext.RunId);
            }
        }

        _ = await runStatusNotifier.TryNotifyAsync(result, exitCode, cancellationToken);

        // 致命的エラーで中断した実行では、完了後の保持期間整理を行わない。
        if (result.FatalError is null)
        {
            dataRetentionService.TryDeleteExpiredData();
        }
    }
}
