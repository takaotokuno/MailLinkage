using MailKit.Security;

namespace TestMailSender.Mail;

internal static class SmtpSecurity
{
    public static SecureSocketOptions ToSecureSocketOptions(bool useSsl)
    {
        // SecureSocketOptions : MailKitのSMTP接続時の暗号化方式を指定するenum
        // SslOnConnect : SSL/TLS接続 最初から暗号化して接続する
        // StartTlsWhenAvailable : STARTTLS 最初は通常接続し、途中でTLSに切り替える
        return useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
