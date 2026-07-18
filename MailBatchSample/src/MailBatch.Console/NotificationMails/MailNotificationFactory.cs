using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.NotificationMails;

/// <summary>
/// 通知メールの件名・本文をテンプレートから組み立てます。
/// </summary>
internal sealed class MailNotificationFactory(MailNotificationOptions notificationOptions, BatchRunContext runContext)
{
    /// <summary>
    /// バッチ実行結果の通知テンプレートから管理者宛て通知を作成します。
    /// </summary>
    public MailNotification CreateRunStatusNotification(BatchRunResult result, int exitCode)
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

    /// <summary>
    /// 実行結果通知テンプレートへバッチ結果を差し込みます。
    /// </summary>
    private string ApplyRunStatusTemplate(string template, BatchRunResult result, int exitCode)
    {
        string status = ToRunStatus(exitCode);
        ProcessResult processResult = result.ProcessResult;
        FatalBatchError? fatalError = result.FatalError;

        return template
            .Replace("{RunId}", runContext.RunId, StringComparison.Ordinal)
            .Replace("{Status}", status, StringComparison.Ordinal)
            .Replace("{ExitCode}", exitCode.ToString(), StringComparison.Ordinal)
            .Replace("{Total}", processResult.Total.ToString(), StringComparison.Ordinal)
            .Replace("{Succeeded}", processResult.Succeeded.ToString(), StringComparison.Ordinal)
            .Replace("{Failed}", processResult.Failed.ToString(), StringComparison.Ordinal)
            .Replace("{InvalidFormat}", processResult.InvalidFormat.ToString(), StringComparison.Ordinal)
            .Replace("{ApiFailed}", processResult.ApiFailed.ToString(), StringComparison.Ordinal)
            .Replace("{FatalErrorCode}", fatalError?.Code ?? string.Empty, StringComparison.Ordinal)
            .Replace("{FatalErrorMessage}", fatalError?.Message ?? string.Empty, StringComparison.Ordinal)
            .Replace("{FatalErrorStage}", fatalError?.Stage ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// 入力エラー通知テンプレートへ検証結果を差し込みます。
    /// </summary>
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

    /// <summary>
    /// 通知に含める本文プレビューを作成します。
    /// </summary>
    private static string CreatePreview(string value)
    {
        const int MAX_PREVIEW_LENGTH = 200;
        return value.Length <= MAX_PREVIEW_LENGTH ? value : $"{value[..MAX_PREVIEW_LENGTH]}...";
    }

    /// <summary>
    /// 終了コードを通知用の実行ステータス文字列へ変換します。
    /// </summary>
    private static string ToRunStatus(int exitCode) => exitCode == 0 ? "succeeded" : "failed";
}
