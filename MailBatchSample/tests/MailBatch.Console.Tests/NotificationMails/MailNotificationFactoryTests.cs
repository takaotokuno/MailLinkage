using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
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
        DateTimeOffset startedAt = new(2026, 7, 19, 10, 20, 30, TimeSpan.Zero);
        DateTimeOffset endedAt = new(2026, 7, 19, 10, 21, 45, TimeSpan.Zero);
        BatchRunResult result = new(
            new ProcessResult(Total: 3, Succeeded: 2, InvalidFormat: 1, ApiFailed: 0),
            startedAt,
            endedAt);

        MailNotification notification = factory.CreateRunStatusNotification(result, exitCode: 1);

        Assert.Equal("admin@example.com", notification.To);
        Assert.Equal("Run run-001 failed", notification.Subject);
        Assert.Equal(
            "StartedAt=2026-07-19T10:20:30.0000000+00:00 EndedAt=2026-07-19T10:21:45.0000000+00:00 Exit=1 Total=3 Succeeded=2 InvalidFormat=1 ApiFailed=0 Fatal=  ",
            notification.Body);
    }

    /// <summary>
    /// 状態: 致命的エラーを含むバッチ実行結果が設定されている。
    /// 振る舞い: 致命的エラー用のプレースホルダーを置換する。
    /// </summary>
    [Fact]
    public void CreateRunStatusNotification_WhenFatalErrorExists_AppliesFatalErrorPlaceholders()
    {
        MailNotificationFactory factory = new(CreateOptions(), new BatchRunContext("run-001"));
        BatchRunResult result = new(
            new ProcessResult(Total: 0),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            new FatalBatchError(
                Code: "DuplicateRun",
                Message: "Another mail batch instance is already running.",
                Stage: "Startup"));

        MailNotification notification = factory.CreateRunStatusNotification(result, exitCode: 1);

        Assert.Equal(
            "StartedAt=1970-01-01T00:00:00.0000000+00:00 EndedAt=1970-01-01T00:00:00.0000000+00:00 Exit=1 Total=0 Succeeded=0 InvalidFormat=0 ApiFailed=0 Fatal=DuplicateRun Another mail batch instance is already running. Startup",
            notification.Body);
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
            MailId: new ReceivedMailId(123, 999),
            From: "sender@example.com",
            Subject: new string('s', 201),
            Body: "body");

        MailNotification notification = factory.CreateValidationErrorNotification(
            mail,
            ["first error", "second error"]);

        Assert.Equal("sender@example.com", notification.To);
        Assert.Equal($"Validation 999:123 {new string('s', 200)}...", notification.Subject);
        Assert.Equal($"Errors:{Environment.NewLine}- first error{Environment.NewLine}- second error", notification.Body);
    }

    [Fact]
    public void CreateMetricAlert_AppliesAlertTemplate()
    {
        MailNotificationFactory factory = new(CreateOptions(), new BatchRunContext("run-001"));

        MailNotification notification = factory.CreateMetricAlert(
            "Stalled mail moves",
            "One or more mail moves require attention.");

        Assert.Equal("admin@example.com", notification.To);
        Assert.Equal("Alert: Stalled mail moves", notification.Subject);
        Assert.Equal("One or more mail moves require attention.", notification.Body);
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
                    Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME,
                    Subject = "Run {RunId} {Status}",
                    Body = "StartedAt={StartedAt} EndedAt={EndedAt} Exit={ExitCode} Total={Total} Succeeded={Succeeded} InvalidFormat={InvalidFormat} ApiFailed={ApiFailed} Fatal={FatalErrorCode} {FatalErrorMessage} {FatalErrorStage}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.VALIDATION_ERROR_TEMPLATE_NAME,
                    Subject = "Validation {MailId} {Subject}",
                    Body = "Errors:\n{ValidationErrors}"
                },
                new MailNotificationTemplateOptions
                {
                    Name = MailNotificationOptions.METRIC_ALERT_TEMPLATE_NAME,
                    Subject = "Alert: {AlertTitle}",
                    Body = "{AlertMessage}"
                }
            ]
        };
    }
}
