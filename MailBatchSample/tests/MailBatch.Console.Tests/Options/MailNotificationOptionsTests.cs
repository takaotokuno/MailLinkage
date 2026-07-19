using MailBatch.Console.Options;
using MailKit.Security;
using Xunit;

namespace MailBatch.Console.Tests.Options;

public sealed class MailNotificationOptionsTests
{
    /// <summary>
    /// 状態: 通知メール設定に必須項目と必要なテンプレートが設定されている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        MailNotificationOptions options = CreateValidOptions();

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// 状態: 通知メール設定の管理者宛先が未設定になっている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Validate_WithMissingAdminAddress_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(adminAddress: string.Empty);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:AdminAddress is required.", exception.Message);
    }

    /// <summary>
    /// 状態: 通知メール接続設定のSocketOptionsに不正な値が設定されている。
    /// 振る舞い: MailKitで扱える値ではないため、Notification:SocketOptionsの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void Validate_WithInvalidSocketOptions_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(socketOptions: (SecureSocketOptions)(-1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:SocketOptions must be a valid SecureSocketOptions value.", exception.Message);
    }

    /// <summary>
    /// 状態: 通知メール設定に検証エラー通知テンプレートが含まれていない。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Validate_WithMissingValidationErrorTemplate_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(templates:
        [
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME,
                Subject = "Mail batch {Status}: RunId={RunId}",
                Body = "RunId: {RunId}"
            }
        ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:Templates requires a template named 'ValidationError'.", exception.Message);
    }

    private static MailNotificationOptions CreateValidOptions(
        string adminAddress = "admin@example.local",
        List<MailNotificationTemplateOptions>? templates = null,
        SecureSocketOptions socketOptions = SecureSocketOptions.SslOnConnect)
    {
        templates ??=
        [
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME,
                Subject = "Mail batch {Status}: RunId={RunId}",
                Body = "RunId: {RunId}"
            },
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.VALIDATION_ERROR_TEMPLATE_NAME,
                Subject = "Received mail validation failed: MailId={MailId}",
                Body = "Validation errors:\n{ValidationErrors}"
            },
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.METRIC_ALERT_TEMPLATE_NAME,
                Subject = "Alert: {AlertTitle}",
                Body = "{AlertMessage}"
            }
        ];

        return new MailNotificationOptions
        {
            SmtpHost = "mailserver",
            SmtpPort = 3025,
            SocketOptions = socketOptions,
            From = "mailbatch@example.local",
            AdminAddress = adminAddress,
            Templates = templates
        };
    }
}
