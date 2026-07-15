using MailKit.Security;

namespace MailBatch.Console.Options;

/// <summary>
/// IMAPサーバー接続と受信メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ImapOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 993;
    public string SecureSocketOption { get; init; } = "SslOnConnect";
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

        if (!Enum.TryParse<SecureSocketOptions>(SecureSocketOption, ignoreCase: true, out _))
        {
            throw new InvalidOperationException(
                "Imap:SecureSocketOption must be a valid SecureSocketOptions value.");
        }
    }

    /// <summary>
    /// 設定文字列をMailKitのSecureSocketOptionsへ変換します。
    /// </summary>
    public SecureSocketOptions GetSecureSocketOptions()
    {
        return Enum.Parse<SecureSocketOptions>(
            SecureSocketOption,
            ignoreCase: true);
    }
}
