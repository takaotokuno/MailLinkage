using MailBatch.Console.Models;
using MailBatch.Console.NotificationMails;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// バッチ実行結果通知の生成と送信を担当します。
/// </summary>
internal sealed class RunStatusNotifier(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory) : IRunStatusNotifier
{
    public async Task NotifyAsync(ProcessResult result, int exitCode)
    {
        await mailNotifier.SendAsync(mailNotificationFactory.CreateRunStatusNotification(result, exitCode));
    }
}
