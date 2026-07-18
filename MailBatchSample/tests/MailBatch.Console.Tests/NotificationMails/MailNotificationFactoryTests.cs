using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.NotificationMails;

public sealed class MailNotificationFactoryTests
{
    /// <summary>
    /// 状態: 実行状態通知テンプレートとバッチ実行結果が設定されている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void CreateRunStatusNotification_AppliesRunStatusTemplate()
    {
        MailNotificationFactory factory = new(CreateOptions(), new BatchRunContext("run-001"));
        ProcessResult result = new(Total: 3, Succeeded: 2, InvalidFormat: 1, ApiFailed: 0);

        MailNotification notification = factory.CreateRunStatusNotification(result, exitCode: 1);

        Assert.Equal("admin@example.com", notification.To);
        Assert.Equal("Run run-001 failed", notification.Subject);
        Assert.Equal("Exit=1 Total=3 Succeeded=2 InvalidFormat=1 ApiFailed=0", notification.Body);
    }

    /// <summary>
    /// 状態: 検証エラー通知テンプレートと検証エラーを持つ受信メールが設定されている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
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

    private static MailNotificationOptions CreateOptions()
    {
        return new MailNotificationOptions
        {
            AdminAddress = "admin@example.com",
            Templates =
            [
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.RunStatusTemplateName,
                    Subject = "Run {RunId} {Status}",
                    Body = "Exit={ExitCode} Total={Total} Succeeded={Succeeded} InvalidFormat={InvalidFormat} ApiFailed={ApiFailed}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.ValidationErrorTemplateName,
                    Subject = "Validation {MailId} {Subject}",
                    Body = "Errors:\n{ValidationErrors}"
                }
            ]
        };
    }
}
