using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
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
        ReceivedMail mail = new(
            MailId: new ReceivedMailId(123),
            Sender: "sender@example.com",
            Subject: new string('s', 201),
            Body: "body");

        MailNotification notification = factory.CreateValidationErrorNotification(
            mail,
            ["first error", "second error"]);

        Assert.Equal("sender@example.com", notification.To);
        Assert.Equal($"Validation 123 {new string('s', 200)}...", notification.Subject);
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
                        Subject = "Validation {MailId} {Subject}",
                        Body = "Errors:\n{ValidationErrors}"
                    }
                ]
            }
        };
    }
}
