using MimeKit;
using TestMailSender.Options;

namespace TestMailSender.Services;

/// <summary>
/// テストメール送信操作を提供します。
/// </summary>
internal interface ITestMailSender
{
    /// <summary>
    /// 指定されたSMTP設定を使用してメールを送信します。
    /// </summary>
    Task SendAsync(SmtpOptions options, MimeMessage message, CancellationToken cancellationToken = default);
}
