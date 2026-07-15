using MailKit.Net.Smtp;
using MimeKit;
using TestMailSender.Options;
using TestMailSender.Services;

namespace TestMailSender.Infrastructure;

/// <summary>
/// SMTPサーバー経由でテストメールを送信します。
/// </summary>
internal sealed class SmtpTestMailSender : ITestMailSender
{
    /// <summary>
    /// 指定されたSMTP設定を使用してメールを送信します。
    /// </summary>
    public async Task SendAsync(
        SmtpOptions options,
        MimeMessage message,
        CancellationToken cancellationToken = default)
    {
        using SmtpClient smtpClient = new();
        await smtpClient.ConnectAsync(
            options.Host,
            options.Port,
            SmtpSecurity.ToSecureSocketOptions(useSsl: true),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.UserName))
        {
            await smtpClient.AuthenticateAsync(options.UserName, options.Password!, cancellationToken);
        }

        _ = await smtpClient.SendAsync(message, cancellationToken);
        await smtpClient.DisconnectAsync(true, cancellationToken);
    }
}
