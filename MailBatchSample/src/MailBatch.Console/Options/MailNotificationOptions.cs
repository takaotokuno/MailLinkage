namespace MailBatch.Console.Options;

/// <summary>
/// 通知メール送信に関する設定を保持します。
/// </summary>
internal sealed class MailNotificationOptions
{
    public const string RunStatusTemplateName = "RunStatus";
    public const string ValidationErrorTemplateName = "ValidationError";

    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 25;
    public string? UserName
    {
        get; init;
    }
    public string? Password
    {
        get; init;
    }
    public string From { get; init; } = string.Empty;
    public string AdminAddress { get; init; } = string.Empty;
    public List<MailNotificationTemplateOptions> Templates { get; init; } = [];

    /// <summary>
    /// 通知メールの送信に必要なSMTP設定、既定の管理者宛先、通知テンプレートを検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(SmtpHost, "Notification:SmtpHost");
        OptionValidation.RequireRange(SmtpPort, 1, 65535, "Notification:SmtpPort");
        OptionValidation.Require(From, "Notification:From");
        OptionValidation.Require(AdminAddress, "Notification:AdminAddress");
        OptionValidation.RequireNotEmpty(Templates, "Notification:Templates");

        for (int idx = 0; idx < Templates.Count; idx++)
        {
            MailNotificationTemplateOptions template = Templates[idx];
            string path = $"Notification:Templates:{idx}";
            template.Validate(path);
        }

        RequireTemplate(RunStatusTemplateName);
        RequireTemplate(ValidationErrorTemplateName);
    }

    /// <summary>
    /// 指定名の通知テンプレート設定を取得します。
    /// </summary>
    public MailNotificationTemplateOptions GetTemplate(string name)
    {
        return Templates.First(template =>
        {
            return string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// 指定名の通知テンプレート設定が有効か検証します。
    /// </summary>
    private void RequireTemplate(string name)
    {
        if (!Templates.Any(template =>
        {
            return string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase);
        }))
        {
            throw new InvalidOperationException($"Notification:Templates requires a template named '{name}'.");
        }
    }
}
