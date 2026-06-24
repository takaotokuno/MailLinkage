using Xunit;
using MailBatch.Console.Mail;
using MailKit.Security;

namespace MailBatch.Console.Tests.Mail;

public sealed class ImapSecurityTests
{
    [Theory]
    [InlineData(true, SecureSocketOptions.SslOnConnect)]
    [InlineData(false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ToSecureSocketOptions_MapsSslFlagToMailKitOption(bool useSsl, SecureSocketOptions expected)
    {
        var option = ImapSecurity.ToSecureSocketOptions(useSsl);

        Assert.Equal(expected, option);
    }
}
