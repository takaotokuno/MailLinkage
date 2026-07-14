using MailBatch.Console.BatchProcessing;

namespace MailBatch.Console.NotificationMails;

internal interface IRunStatusNotifier
{
    Task NotifyAsync(ProcessResult result, int exitCode, CancellationToken cancellationToken = default);
}

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
