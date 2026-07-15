namespace MailBatch.Console.Options;

/// <summary>
/// 通知メールテンプレートの名前、件名、本文を保持します。
/// </summary>
internal sealed class MailNotificationTemplateOptions
{
    public string Name { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// テンプレート設定の必須項目を検証します。
    /// </summary>
    public void Validate(string path)
    {
        OptionValidation.Require(Name, $"{path}:Name");
        OptionValidation.Require(Subject, $"{path}:Subject");
        OptionValidation.Require(Body, $"{path}:Body");
    }
}
