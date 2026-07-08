namespace MailBatch.Console.Options;

internal sealed class MailNotificationOptions
{
    public const string RunStatusTemplateName = "RunStatus";
    public const string ValidationErrorTemplateName = "ValidationError";

    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 25;
    public bool UseSsl { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
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

        for (int index = 0; index < Templates.Count; index++)
        {
            MailNotificationTemplateOptions template = Templates[index];
            string path = $"Notification:Templates:{index}";
            OptionValidation.Require(template.Name, $"{path}:Name");
            OptionValidation.Require(template.Subject, $"{path}:Subject");
            OptionValidation.Require(template.Body, $"{path}:Body");
        }

        RequireTemplate(RunStatusTemplateName);
        RequireTemplate(ValidationErrorTemplateName);
    }

    public MailNotificationTemplateOptions GetTemplate(string name)
    {
        return Templates.First(template => string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void RequireTemplate(string name)
    {
        if (!Templates.Any(template => string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Notification:Templates requires a template named '{name}'.");
        }
    }
}

internal sealed class MailNotificationTemplateOptions
{
    public string Name { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
}
