using MailKit.Security;

namespace MailBatch.Console.Mail;

internal static class ImapSecurity
{
    /// <summary>
    /// SSL利用設定に応じたIMAP接続用のセキュアソケットオプションを返します。
    /// </summary>
    public static SecureSocketOptions ToSecureSocketOptions(bool useSsl)
    {
        return useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
