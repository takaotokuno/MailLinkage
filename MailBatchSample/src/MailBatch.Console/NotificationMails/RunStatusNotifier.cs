using MailBatch.Console.BatchProcessing.Result;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.NotificationMails;

/// <summary>
/// バッチ実行結果を通知する操作を提供します。
/// </summary>
internal interface IRunStatusNotifier
{
    /// <summary>
    /// バッチ実行結果と終了コードから実行結果通知を送信します。
    /// </summary>
    Task<bool> TryNotifyAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// バッチ実行結果通知を作成してメール送信します。
/// </summary>
internal sealed class RunStatusNotifier(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<RunStatusNotifier> logger) : IRunStatusNotifier
{
    /// <summary>
    /// バッチ実行結果と終了コードから実行結果通知を作成し、送信に成功したかどうかを返します。
    /// </summary>
    public async Task<bool> TryNotifyAsync(BatchRunResult result, int exitCode, CancellationToken cancellationToken = default)
    {
        MailNotification notification = mailNotificationFactory.CreateRunStatusNotification(result, exitCode);
        try
        {
            await mailNotifier.SendAsync(notification, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to send run status notification. ExitCode={ExitCode}",
                exitCode);
            return false;
        }
    }
}
