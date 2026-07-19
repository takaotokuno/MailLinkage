using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class RunStatusNotifierTests
{
    /// <summary>
    /// 状態: 実行状態通知メールを送信できる。
    /// 振る舞い: trueを返す。
    /// </summary>
    [Fact]
    public async Task TryNotifyAsync_WhenMailIsSent_ReturnsTrue()
    {
        FakeMailNotifier mailNotifier = new();
        RunStatusNotifier notifier = new(
            mailNotifier,
            CreateFactory(),
            NullLogger<RunStatusNotifier>.Instance);

        bool notified = await notifier.TryNotifyAsync(CreateRunResult(), exitCode: 0);

        Assert.True(notified);
        _ = Assert.Single(mailNotifier.Notifications);
    }

    /// <summary>
    /// 状態: 実行状態通知メールの送信に失敗する。
    /// 振る舞い: 例外を呼び出し元へ伝播せず、falseを返す。
    /// </summary>
    [Fact]
    public async Task TryNotifyAsync_WhenMailSendingFails_ReturnsFalse()
    {
        FakeMailNotifier mailNotifier = new(new InvalidOperationException("SMTP unavailable"));
        RunStatusNotifier notifier = new(
            mailNotifier,
            CreateFactory(),
            NullLogger<RunStatusNotifier>.Instance);

        bool notified = await notifier.TryNotifyAsync(CreateRunResult(), exitCode: 1);

        Assert.False(notified);
    }

    private static BatchRunResult CreateRunResult() => new(new ProcessResult(Total: 1, Succeeded: 1));

    private static MailNotificationFactory CreateFactory()
    {
        MailNotificationOptions options = new()
        {
            AdminAddress = "admin@example.com",
            Templates =
            [
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME,
                    Subject = "Run {RunId} {Status}",
                    Body = "Exit={ExitCode}"
                }
            ]
        };

        return new MailNotificationFactory(options, new BatchRunContext("run-001"));
    }

    private sealed class FakeMailNotifier(Exception? exception = null) : IMailNotifier
    {
        public List<MailNotification> Notifications { get; } = [];

        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }
    }
}
