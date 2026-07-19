using MailKit.Security;

namespace MailBatch.Console.Options;

/// <summary>
/// IMAPサーバー接続と受信メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ImapOptions
{
    private const int DEFAULT_PORT = 993;
    private const int DEFAULT_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_SECONDS = 2;

    public string Host { get; init; } = "";
    public int Port { get; init; } = DEFAULT_PORT;
    public SecureSocketOptions SocketOptions { get; init; } = SecureSocketOptions.SslOnConnect;
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Mailbox { get; init; } = "INBOX";
    public int RetryCount { get; init; } = DEFAULT_RETRY_COUNT;
    public int RetryDelaySeconds { get; init; } = DEFAULT_RETRY_DELAY_SECONDS;

    /// <summary>
    /// IMAP接続に必要な設定値を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(Host, "Imap:Host");
        OptionValidation.RequireRange(
            Port,
            OptionValidation.MINIMUM_NETWORK_PORT,
            OptionValidation.MAXIMUM_NETWORK_PORT,
            "Imap:Port");
        OptionValidation.Require(UserName, "Imap:UserName");
        OptionValidation.Require(Password, "Imap:Password");
        OptionValidation.Require(Mailbox, "Imap:Mailbox");
        OptionValidation.RequireNonNegative(RetryCount, "Imap:RetryCount");
        OptionValidation.RequirePositive(RetryDelaySeconds, "Imap:RetryDelaySeconds");

        OptionValidation.RequireDefined(SocketOptions, "Imap:SocketOptions");
    }
}
