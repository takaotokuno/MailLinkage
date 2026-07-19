using MailKit.Security;

namespace MailBatch.Console.Options;

/// <summary>
/// 通知メール送信に関する設定を保持します。
/// </summary>
internal sealed class MailNotificationOptions
{
    private const int DEFAULT_SMTP_PORT = 25;

    /// <summary>バッチ実行結果通知に使用するテンプレート名です。</summary>
    public const string RUN_STATUS_TEMPLATE_NAME = "RunStatus";
    /// <summary>入力検証エラー通知に使用するテンプレート名です。</summary>
    public const string VALIDATION_ERROR_TEMPLATE_NAME = "ValidationError";
    /// <summary>メトリクスアラート通知に使用するテンプレート名です。</summary>
    public const string METRIC_ALERT_TEMPLATE_NAME = "MetricAlert";

    /// <summary>SMTPサーバーのホスト名を取得します。</summary>
    public string SmtpHost { get; init; } = string.Empty;
    /// <summary>SMTPサーバーのポート番号を取得します。</summary>
    public int SmtpPort { get; init; } = DEFAULT_SMTP_PORT;
    /// <summary>SMTP接続で使用するSSL/TLS方式を取得します。</summary>
    public SecureSocketOptions SocketOptions { get; init; } = SecureSocketOptions.SslOnConnect;
    /// <summary>SMTP認証に使用するユーザー名を取得します。</summary>
    public string? UserName
    {
        get; init;
    }
    /// <summary>SMTP認証に使用するパスワードを取得します。</summary>
    public string? Password
    {
        get; init;
    }
    /// <summary>通知メールの送信元アドレスを取得します。</summary>
    public string From { get; init; } = string.Empty;
    /// <summary>通知メールの既定の管理者宛先を取得します。</summary>
    public string AdminAddress { get; init; } = string.Empty;
    /// <summary>用途別の通知メールテンプレートを取得します。</summary>
    public List<MailNotificationTemplateOptions> Templates { get; init; } = [];

    /// <summary>
    /// 通知メールの送信に必要なSMTP設定、既定の管理者宛先、通知テンプレートを検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(SmtpHost, "Notification:SmtpHost");
        OptionValidation.RequireRange(
            SmtpPort,
            OptionValidation.MINIMUM_NETWORK_PORT,
            OptionValidation.MAXIMUM_NETWORK_PORT,
            "Notification:SmtpPort");
        OptionValidation.RequireDefined(SocketOptions, "Notification:SocketOptions");
        OptionValidation.Require(From, "Notification:From");
        OptionValidation.Require(AdminAddress, "Notification:AdminAddress");
        OptionValidation.RequireNotEmpty(Templates, "Notification:Templates");

        for (int idx = 0; idx < Templates.Count; idx++)
        {
            MailNotificationTemplateOptions template = Templates[idx];
            string path = $"Notification:Templates:{idx}";
            template.Validate(path);
        }

        RequireTemplate(RUN_STATUS_TEMPLATE_NAME);
        RequireTemplate(VALIDATION_ERROR_TEMPLATE_NAME);
        RequireTemplate(METRIC_ALERT_TEMPLATE_NAME);
    }

    /// <summary>
    /// 指定名の通知テンプレート設定を取得します。
    /// </summary>
    public MailNotificationTemplateOptions GetTemplate(string name)
    {
        return Templates.First(template =>
            string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 指定名の通知テンプレート設定が有効か検証します。
    /// </summary>
    private void RequireTemplate(string name)
    {
        if (!Templates.Any(template =>
            string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Notification:Templates requires a template named '{name}'.");
        }
    }
}
