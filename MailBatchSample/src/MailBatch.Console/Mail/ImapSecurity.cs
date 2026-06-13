using MailKit.Security;

namespace MailBatch.Console.Mail;

internal static class ImapSecurity
{
    public static SecureSocketOptions ToSecureSocketOptions(bool useSsl)
    {
        return useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
