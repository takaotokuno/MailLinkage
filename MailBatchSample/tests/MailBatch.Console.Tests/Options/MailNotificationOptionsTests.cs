using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.Options;

public sealed class MailNotificationOptionsTests
{
    /// <summary>
    /// 状態: 管理者宛先と通知テンプレートを含む有効な通知オプションを設定する。
    /// 振る舞い: 検証で例外を投げない。
    /// </summary>
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        MailNotificationOptions options = CreateValidOptions();

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// 状態: 管理者宛先が未設定の通知オプションを設定する。
    /// 振る舞い: 検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Validate_WithMissingAdminAddress_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(adminAddress: string.Empty);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:AdminAddress is required.", exception.Message);
    }

    /// <summary>
    /// 状態: 検証エラー通知テンプレートが未設定の通知オプションを設定する。
    /// 振る舞い: 検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Validate_WithMissingValidationErrorTemplate_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(templates:
        [
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.RunStatusTemplateName,
                Subject = "Mail batch {Status}: RunId={RunId}",
                Body = "RunId: {RunId}"
            }
        ]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:Templates requires a template named 'ValidationError'.", exception.Message);
    }

    private static MailNotificationOptions CreateValidOptions(
        string adminAddress = "admin@example.local",
        List<MailNotificationTemplateOptions>? templates = null)
    {
        templates ??=
        [
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.RunStatusTemplateName,
                Subject = "Mail batch {Status}: RunId={RunId}",
                Body = "RunId: {RunId}"
            },
            new MailNotificationTemplateOptions
            {
                Name = MailNotificationOptions.ValidationErrorTemplateName,
                Subject = "Received mail validation failed: MailId={MailId}",
                Body = "Validation errors:\n{ValidationErrors}"
            }
        ];

        return new MailNotificationOptions
        {
            SmtpHost = "mailserver",
            SmtpPort = 3025,
            From = "mailbatch@example.local",
            AdminAddress = adminAddress,
            Templates = templates
        };
    }
}
