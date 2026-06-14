using MailKit.Security;

namespace TestMailSender.Mail;

internal static class SmtpSecurity
{
    public static SecureSocketOptions ToSecureSocketOptions(bool useSsl)
    {
        return useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
