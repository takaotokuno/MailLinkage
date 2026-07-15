using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.Options;

public sealed class MailNotificationOptionsTests
{
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        MailNotificationOptions options = CreateValidOptions();

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithMissingAdminAddress_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = CreateValidOptions(adminAddress: string.Empty);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:AdminAddress is required.", exception.Message);
    }

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
