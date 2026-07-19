namespace MailBatch.Console.Options;

/// <summary>
/// 通知メールテンプレートの名前、件名、本文を保持します。
/// </summary>
internal sealed class MailNotificationTemplateOptions
{
    /// <summary>テンプレートを識別する名前を取得します。</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>通知メールの件名テンプレートを取得します。</summary>
    public string Subject { get; init; } = string.Empty;
    /// <summary>通知メールの本文テンプレートを取得します。</summary>
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
