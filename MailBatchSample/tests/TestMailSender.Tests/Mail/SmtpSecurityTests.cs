using Xunit;
using MailKit.Security;
using TestMailSender.Mail;

namespace TestMailSender.Tests.Mail;

public sealed class SmtpSecurityTests
{
    // SSL 使用フラグに応じて SMTP 接続の SecureSocketOptions が正しく選択されることを確認する。
    [Theory]
    [InlineData(true, SecureSocketOptions.SslOnConnect)]
    [InlineData(false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ToSecureSocketOptions_MapsSslFlagToMailKitOption(bool useSsl, SecureSocketOptions expected)
    {
        var option = SmtpSecurity.ToSecureSocketOptions(useSsl);

        Assert.Equal(expected, option);
    }
}
