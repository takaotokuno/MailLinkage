using MailBatch.Console.Models;
using MailBatch.Console.NotificationMails;

namespace MailBatch.Console.BatchProcessing;

internal sealed class RunStatusNotifier(
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory) : IRunStatusNotifier
{
    public async Task NotifyAsync(ProcessResult result, int exitCode, CancellationToken cancellationToken = default)
    {
        MailNotification notification = mailNotificationFactory.CreateRunStatusNotification(result, exitCode);
        await mailNotifier.SendAsync(notification, cancellationToken);
    }
}
