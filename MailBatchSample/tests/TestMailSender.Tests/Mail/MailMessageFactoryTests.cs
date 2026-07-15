using TestMailSender.Mail;
using TestMailSender.Options;
using Xunit;

namespace TestMailSender.Tests.Mail;

public sealed class MailMessageFactoryTests
{
    /// <summary>
    /// 状態: メール送信モードに対応する件名設定を用意する。
    /// 振る舞い: 指定モードに対応する件名でメールを作成する。
    /// </summary>
    [Theory]
    [InlineData("target", "Target subject")]
    [InlineData("nontarget", "Non-target subject")]
    [InlineData("non-target", "Non-target subject")]
    [InlineData("duplicate", "Target subject")]
    public void Create_SelectsSubjectFromConfiguredMode(string mode, string expectedSubject)
    {
        AppOptions options = CreateOptions(mode);

        MimeKit.MimeMessage message = MailMessageFactory.Create(options);

        Assert.Equal(expectedSubject, message.Subject);
        Assert.Equal("sender@example.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("receiver@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("mail body", message.TextBody);
    }

    /// <summary>
    /// 状態: 重複モードと固定 MessageId を設定する。
    /// 振る舞い: 設定済みの重複用 MessageId でメールを作成する。
    /// </summary>
    [Fact]
    public void Create_UsesConfiguredDuplicateMessageIdForDuplicateMode()
    {
        AppOptions options = CreateOptions("duplicate");

        MimeKit.MimeMessage message = MailMessageFactory.Create(options);

        Assert.Equal("duplicate-message-id@example.com", message.MessageId);
    }

    /// <summary>
    /// 状態: カスタムモードとカスタム件名を設定する。
    /// 振る舞い: カスタム件名でメールを作成する。
    /// </summary>
    [Fact]
    public void Create_UsesCustomSubjectWhenModeIsCustom()
    {
        AppOptions options = CreateOptions("custom");

        MimeKit.MimeMessage message = MailMessageFactory.Create(options);

        Assert.Equal("Custom subject", message.Subject);
        Assert.EndsWith("@example.local", message.MessageId);
    }

    /// <summary>
    /// 状態: カスタムモードで件名を設定しない。
    /// 振る舞い: メール作成時に InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Create_ThrowsWhenCustomModeDoesNotConfigureSubject()
    {
        AppOptions options = CreateOptions("custom", subject: null);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            return MailMessageFactory.Create(options);
        });

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
