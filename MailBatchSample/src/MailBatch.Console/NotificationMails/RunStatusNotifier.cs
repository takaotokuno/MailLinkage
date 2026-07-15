using MailBatch.Console.BatchProcessing;

namespace MailBatch.Console.NotificationMails;

/// <summary>
/// バッチ実行結果を通知する操作を提供します。
/// </summary>
internal interface IRunStatusNotifier
{
    /// <summary>
    /// 処理結果と終了コードから実行結果通知を送信します。
    /// </summary>
    Task NotifyAsync(ProcessResult result, int exitCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// バッチ実行結果通知を作成してメール送信します。
/// </summary>
internal sealed class RunStatusNotifier(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory) : IRunStatusNotifier
{
    /// <summary>
    /// 処理結果と終了コードから実行結果通知を作成し送信します。
    /// </summary>
    public async Task NotifyAsync(ProcessResult result, int exitCode, CancellationToken cancellationToken = default)
    {
        MailNotification notification = mailNotificationFactory.CreateRunStatusNotification(result, exitCode);
        await mailNotifier.SendAsync(notification, cancellationToken);
    }
}
