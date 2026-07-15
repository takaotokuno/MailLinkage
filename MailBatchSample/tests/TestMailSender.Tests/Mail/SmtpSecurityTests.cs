using MailKit.Security;
using TestMailSender.Infrastructure;
using Xunit;

namespace TestMailSender.Tests.Mail;

public sealed class SmtpSecurityTests
{
    /// <summary>
    /// 状態: SMTP の SSL 利用フラグを指定する。
    /// 振る舞い: フラグに対応する MailKit のセキュアソケット設定へ変換する。
    /// </summary>
    [Theory]
    [InlineData(true, SecureSocketOptions.SslOnConnect)]
    [InlineData(false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ToSecureSocketOptions_MapsSslFlagToMailKitOption(bool useSsl, SecureSocketOptions expected)
    {
        SecureSocketOptions option = SmtpSecurity.ToSecureSocketOptions(useSsl);

        Assert.Equal(expected, option);
    }
}
