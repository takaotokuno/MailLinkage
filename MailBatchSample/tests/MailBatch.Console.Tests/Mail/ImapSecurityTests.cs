using Xunit;
using MailBatch.Console.Mail;
using MailKit.Security;

namespace MailBatch.Console.Tests.Mail;

public sealed class ImapSecurityTests
{
    // SSL 使用フラグに応じて IMAP 接続の SecureSocketOptions が正しく選択されることを確認する。
    [Theory]
    [InlineData(true, SecureSocketOptions.SslOnConnect)]
    [InlineData(false, SecureSocketOptions.StartTlsWhenAvailable)]
    public void ToSecureSocketOptions_MapsSslFlagToMailKitOption(bool useSsl, SecureSocketOptions expected)
    {
        var option = ImapSecurity.ToSecureSocketOptions(useSsl);

        Assert.Equal(expected, option);
    }
}
