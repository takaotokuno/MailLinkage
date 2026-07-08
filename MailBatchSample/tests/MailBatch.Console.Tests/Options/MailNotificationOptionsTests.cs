using MailBatch.Console.Options;

namespace MailBatch.Console.Tests.Options;

public sealed class MailNotificationOptionsTests
{
    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        MailNotificationOptions options = new()
        {
            SmtpHost = "mailserver",
            SmtpPort = 3025,
            From = "mailbatch@example.local",
            AdminAddress = "admin@example.local",
            SubjectTemplate = "Mail batch {Status}: RunId={RunId}",
            BodyTemplate = "RunId: {RunId}"
        };

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithMissingAdminAddress_ThrowsInvalidOperationException()
    {
        MailNotificationOptions options = new()
        {
            SmtpHost = "mailserver",
            SmtpPort = 3025,
            From = "mailbatch@example.local",
            SubjectTemplate = "Mail batch {Status}: RunId={RunId}",
            BodyTemplate = "RunId: {RunId}"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Notification:AdminAddress is required.", exception.Message);
    }
}
