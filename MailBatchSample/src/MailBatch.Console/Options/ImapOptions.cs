namespace MailBatch.Console.Options;

internal sealed class ImapOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 993;
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Mailbox { get; init; } = "INBOX";

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(Host, "Imap:Host");
        OptionValidation.Require(UserName, "Imap:UserName");
        OptionValidation.Require(Password, "Imap:Password");
        OptionValidation.Require(Mailbox, "Imap:Mailbox");
        OptionValidation.RequireRange(Port, 1, 65535, "Imap:Port");
    }
}
