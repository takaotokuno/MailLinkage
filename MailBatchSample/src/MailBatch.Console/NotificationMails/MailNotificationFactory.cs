using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.NotificationMails;

internal sealed class MailNotificationFactory(MailNotificationOptions notificationOptions, BatchRunContext runContext)
{
    /// <summary>
    /// バッチ実行結果の通知テンプレートから管理者宛て通知を作成します。
    /// </summary>
    public MailNotification CreateRunStatusNotification(ProcessResult result, int exitCode)
    {
        MailNotificationTemplateOptions template
            = notificationOptions.GetTemplate(MailNotificationOptions.RunStatusTemplateName);

        return new MailNotification(
            notificationOptions.AdminAddress,
            ApplyRunStatusTemplate(template.Subject, result, exitCode),
            ApplyRunStatusTemplate(template.Body, result, exitCode));
    }

    /// <summary>
    /// バリデーションエラー通知テンプレートからメール送信元宛て通知を作成します。
    /// </summary>
    public MailNotification CreateValidationErrorNotification(
        ReceivedMail mail,
        IReadOnlyList<string> validationErrors)
    {
        MailNotificationTemplateOptions template
            = notificationOptions.GetTemplate(MailNotificationOptions.ValidationErrorTemplateName);

        string validationErrorsText = string.Join(Environment.NewLine, validationErrors.Select(error =>
        {
            return $"- {error}";
        }));

        return new MailNotification(
            mail.Sender,
            ApplyValidationErrorTemplate(template.Subject, mail, validationErrorsText),
            ApplyValidationErrorTemplate(template.Body, mail, validationErrorsText));
    }

    private string ApplyRunStatusTemplate(string template, ProcessResult result, int exitCode)
    {
        string status = ToRunStatus(exitCode);

        return template
            .Replace("{RunId}", runContext.RunId, StringComparison.Ordinal)
            .Replace("{Status}", status, StringComparison.Ordinal)
            .Replace("{ExitCode}", exitCode.ToString(), StringComparison.Ordinal)
            .Replace("{Total}", result.Total.ToString(), StringComparison.Ordinal)
            .Replace("{Succeeded}", result.Succeeded.ToString(), StringComparison.Ordinal)
            .Replace("{Failed}", result.Failed.ToString(), StringComparison.Ordinal);
    }

    private static string ApplyValidationErrorTemplate(
        string template,
        ReceivedMail mail,
        string validationErrors)
    {
        return template
            .Replace("{MailId}", mail.MailId.ToString(), StringComparison.Ordinal)
            .Replace("{Subject}", CreatePreview(mail.Subject), StringComparison.Ordinal)
            .Replace("{ValidationErrors}", validationErrors, StringComparison.Ordinal);
    }

    private static string CreatePreview(string value)
    {
        const int MAX_PREVIEW_LENGTH = 200;
        return value.Length <= MAX_PREVIEW_LENGTH ? value : $"{value[..MAX_PREVIEW_LENGTH]}...";
    }

    private static string ToRunStatus(int exitCode) => exitCode == 0 ? "succeeded" : "failed";
}
