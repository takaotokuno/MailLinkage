using MailKit.Security;

namespace MailBatch.Console.Options;

internal sealed class ImapOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 993;
    public string SecureSocketOption { get; init; } = "SslOnConnect";
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string Mailbox { get; init; } = "INBOX";

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

    public SecureSocketOptions GetSecureSocketOptions()
    {
        return Enum.Parse<SecureSocketOptions>(
            SecureSocketOption,
            ignoreCase: true);
    }
}
