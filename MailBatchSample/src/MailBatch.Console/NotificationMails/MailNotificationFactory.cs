using MailBatch.Console.BatchProcessing;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.Options;

namespace MailBatch.Console.NotificationMails;

internal sealed class MailNotificationFactory(AppOptions options, BatchRunContext runContext)
{
    /// <summary>
    /// バッチ実行結果の通知テンプレートから管理者宛て通知を作成します。
    /// </summary>
    public MailNotification CreateRunStatusNotification(ProcessResult result, int exitCode)
    {
        MailNotificationTemplateOptions template = options.Notification.GetTemplate(MailNotificationOptions.RunStatusTemplateName);

        return new MailNotification(
            options.Notification.AdminAddress,
            ApplyRunStatusTemplate(template.Subject, result, exitCode),
            ApplyRunStatusTemplate(template.Body, result, exitCode));
    }

    /// <summary>
    /// バリデーションエラー通知テンプレートからメール送信元宛て通知を作成します。
    /// </summary>
    public MailNotification CreateValidationErrorNotification(
        ReceivedMailContent content,
        IReadOnlyList<string> validationErrors)
    {
        MailNotificationTemplateOptions template = options.Notification.GetTemplate(MailNotificationOptions.ValidationErrorTemplateName);
        string validationErrorsText = string.Join(Environment.NewLine, validationErrors.Select(error => $"- {error}"));

        return new MailNotification(
            content.Sender,
            ApplyValidationErrorTemplate(template.Subject, content, validationErrorsText),
            ApplyValidationErrorTemplate(template.Body, content, validationErrorsText));
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
        ReceivedMailContent content,
        string validationErrors)
    {
        return template
            .Replace("{MessageId}", content.MailId.ToString(), StringComparison.Ordinal)
            .Replace("{Subject}", CreatePreview(content.Subject), StringComparison.Ordinal)
            .Replace("{ValidationErrors}", validationErrors, StringComparison.Ordinal);
    }

    private static string CreatePreview(string value)
    {
        const int maxPreviewLength = 200;
        return value.Length <= maxPreviewLength ? value : $"{value[..maxPreviewLength]}...";
    }

    private static string ToRunStatus(int exitCode)
    {
        return exitCode == 0 ? "succeeded" : "failed";
    }
}
