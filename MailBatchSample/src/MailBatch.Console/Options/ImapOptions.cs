using MailKit.Security;

namespace MailBatch.Console.Options;

/// <summary>
/// IMAPサーバー接続と受信メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ImapOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 993;
    public SecureSocketOptions SocketOptions { get; init; } = SecureSocketOptions.SslOnConnect;
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Mailbox { get; init; } = "INBOX";

    /// <summary>
    /// IMAP接続に必要な設定値を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(Host, "Imap:Host");
        OptionValidation.RequireRange(Port, 1, 65535, "Imap:Port");
        OptionValidation.Require(UserName, "Imap:UserName");
        OptionValidation.Require(Password, "Imap:Password");
        OptionValidation.Require(Mailbox, "Imap:Mailbox");

        OptionValidation.RequireDefined(SocketOptions, "Imap:SocketOptions");
    }
}
