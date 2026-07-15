using MailKit.Security;

namespace TestMailSender.Infrastructure;

/// <summary>
/// SMTP接続時のセキュリティ設定を選択します。
/// </summary>
internal static class SmtpSecurity
{
    /// <summary>
    /// SSL使用フラグに応じてMailKitのSecureSocketOptionsへ変換します。
    /// </summary>
    public static SecureSocketOptions ToSecureSocketOptions(bool useSsl)
    {
        return useSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
    }
}
