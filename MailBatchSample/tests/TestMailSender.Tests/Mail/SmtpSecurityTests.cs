using Xunit;
using MailKit.Security;
using TestMailSender.Mail;

namespace TestMailSender.Tests.Mail;

public sealed class SmtpSecurityTests
{
    [Theory]
    [InlineData(true, SecureSocketOptions.SslOnConnect)]
    [InlineData(false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ToSecureSocketOptions_MapsSslFlagToMailKitOption(bool useSsl, SecureSocketOptions expected)
    {
        var option = SmtpSecurity.ToSecureSocketOptions(useSsl);

        Assert.Equal(expected, option);
    }
}
