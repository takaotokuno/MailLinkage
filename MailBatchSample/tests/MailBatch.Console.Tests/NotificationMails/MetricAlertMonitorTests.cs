using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class MetricAlertMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryCheckMailMoveStagnationAsync_WhenUnrecoveredForSevenDays_SendsAlert()
    {
        FakeMailNotifier mailNotifier = new();
        MetricAlertMonitor notifier = CreateNotifier(mailNotifier);
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            Now.AddDays(-7),
            Now);

        bool notified = await notifier.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        MailNotification notification = Assert.Single(mailNotifier.Notifications);
        Assert.Equal("Alert: Stalled mail moves", notification.Subject);
        Assert.Contains("999:123", notification.Body);
    }

    [Fact]
    public async Task TryCheckMailMoveStagnationAsync_WhenFailureIsNewerThanSevenDays_DoesNotSendAlert()
    {
        FakeMailNotifier mailNotifier = new();
        MetricAlertMonitor notifier = CreateNotifier(mailNotifier);
        MailMoveFailure failure = new(
            new ReceivedMailId(123, 999),
            MailMoveFailureDestination.Processed,
            Now.AddDays(-7).AddTicks(1),
            Now);

        bool notified = await notifier.TryCheckMailMoveStagnationAsync([failure]);

        Assert.True(notified);
        Assert.Empty(mailNotifier.Notifications);
    }

    private static MetricAlertMonitor CreateNotifier(FakeMailNotifier mailNotifier)
    {
        MailNotificationOptions options = new()
        {
            AdminAddress = "admin@example.com",
            Templates =
            [
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.METRIC_ALERT_TEMPLATE_NAME,
                    Subject = "Alert: {AlertTitle}",
                    Body = "{AlertMessage}"
                }
            ]
        };
        MailNotificationFactory factory = new(options, new BatchRunContext("run-001"));
        return new MetricAlertMonitor(
            mailNotifier,
            factory,
            new FixedTimeProvider(Now),
            NullLogger<MetricAlertMonitor>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeMailNotifier : IMailNotifier
    {
        public List<MailNotification> Notifications { get; } = [];

        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
