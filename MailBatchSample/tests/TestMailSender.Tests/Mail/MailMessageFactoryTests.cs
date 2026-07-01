using Xunit;
using TestMailSender.Mail;
using TestMailSender.Options;

namespace TestMailSender.Tests.Mail;

public sealed class MailMessageFactoryTests
{
    // 送信モードに応じて設定済みの件名が選択され、差出人・宛先・本文も反映されることを確認する。
    [Theory]
    [InlineData("target", "Target subject")]
    [InlineData("nontarget", "Non-target subject")]
    [InlineData("non-target", "Non-target subject")]
    [InlineData("duplicate", "Target subject")]
    public void Create_SelectsSubjectFromConfiguredMode(string mode, string expectedSubject)
    {
        var options = CreateOptions(mode);

        var message = MailMessageFactory.Create(options);

        Assert.Equal(expectedSubject, message.Subject);
        Assert.Equal("sender@example.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("receiver@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("mail body", message.TextBody);
    }

    // duplicate モードでは設定済みの重複用 MessageId が使われることを確認する。
    [Fact]
    public void Create_UsesConfiguredDuplicateMessageIdForDuplicateMode()
    {
        var options = CreateOptions("duplicate");

        var message = MailMessageFactory.Create(options);

        Assert.Equal("duplicate-message-id@example.com", message.MessageId);
    }

    // custom モードでは任意件名を使い、一意な MessageId が生成されることを確認する。
    [Fact]
    public void Create_UsesCustomSubjectWhenModeIsCustom()
    {
        var options = CreateOptions("custom");

        var message = MailMessageFactory.Create(options);

        Assert.Equal("Custom subject", message.Subject);
        Assert.EndsWith("@example.local", message.MessageId);
    }

    // custom モードで件名が未設定の場合に設定不備として例外を投げることを確認する。
    [Fact]
    public void Create_ThrowsWhenCustomModeDoesNotConfigureSubject()
    {
        var options = CreateOptions("custom", subject: null);

        var exception = Assert.Throws<InvalidOperationException>(() => MailMessageFactory.Create(options));

        Assert.Equal("Mail:Subject is required when Mail:Mode is custom.", exception.Message);
    }

    private static AppOptions CreateOptions(string mode, string? subject = "Custom subject") => new()
    {
        Mail = new MailOptions
        {
            From = "sender@example.com",
            To = "receiver@example.com",
            Mode = mode,
            TargetSubject = "Target subject",
            NonTargetSubject = "Non-target subject",
            Subject = subject,
            Body = "mail body",
            DuplicateMessageId = "duplicate-message-id@example.com"
        }
    };
}
