using MailBatch.Console.ReceivedMails;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Models;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class MailNotificationFactoryTests
{
    [Fact]
    public void CreateRunStatusNotification_AppliesRunStatusTemplate()
    {
        MailNotificationFactory factory = new(CreateOptions(), new BatchRunContext("run-001"));
        ProcessResult result = new(Total: 3, Succeeded: 2, Failed: 1);

        MailNotification notification = factory.CreateRunStatusNotification(result, exitCode: 1);

        Assert.Equal("admin@example.com", notification.To);
        Assert.Equal("Run run-001 failed", notification.Subject);
        Assert.Equal("Exit=1 Total=3 Succeeded=2 Failed=1", notification.Body);
    }

    [Fact]
    public void CreateValidationErrorNotification_AppliesValidationErrorTemplate()
    {
        MailNotificationFactory factory = new(CreateOptions(), new BatchRunContext("run-001"));
        ReceivedMailRequest request = new(
            MessageId: "<message@example.com>",
            Sender: "sender@example.com",
            Subject: new string('s', 201),
            Body: "body",
            ReceivedAt: DateTimeOffset.UnixEpoch);

        MailNotification notification = factory.CreateValidationErrorNotification(
            request,
            ["first error", "second error"]);

        Assert.Equal("sender@example.com", notification.To);
        Assert.Equal($"Validation <message@example.com> {new string('s', 200)}...", notification.Subject);
        Assert.Equal($"Errors:{Environment.NewLine}- first error{Environment.NewLine}- second error", notification.Body);
    }

    private static AppOptions CreateOptions()
    {
        return new AppOptions
        {
            Notification = new MailNotificationOptions
            {
                AdminAddress = "admin@example.com",
                Templates =
                [
                    new MailNotificationTemplateOptions
                    {
                        Name = MailNotificationOptions.RunStatusTemplateName,
                        Subject = "Run {RunId} {Status}",
                        Body = "Exit={ExitCode} Total={Total} Succeeded={Succeeded} Failed={Failed}"
                    },
                    new MailNotificationTemplateOptions
                    {
                        Name = MailNotificationOptions.ValidationErrorTemplateName,
                        Subject = "Validation {MessageId} {Subject}",
                        Body = "Errors:{ValidationErrors}"
                    }
                ]
            }
        };
    }
}
