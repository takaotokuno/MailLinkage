namespace MailBatch.Console.Options;

internal sealed class MailNotificationOptions
{
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 25;
    public bool UseSsl { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public string From { get; init; } = string.Empty;
    public string AdminAddress { get; init; } = string.Empty;
    public string SubjectTemplate { get; init; } = string.Empty;
    public string BodyTemplate { get; init; } = string.Empty;

    /// <summary>
    /// 通知メールの送信に必要なSMTP設定と既定の管理者宛先を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(SmtpHost, "Notification:SmtpHost");
        OptionValidation.RequireRange(SmtpPort, 1, 65535, "Notification:SmtpPort");
        OptionValidation.Require(From, "Notification:From");
        OptionValidation.Require(AdminAddress, "Notification:AdminAddress");
        OptionValidation.Require(SubjectTemplate, "Notification:SubjectTemplate");
        OptionValidation.Require(BodyTemplate, "Notification:BodyTemplate");
    }
}
