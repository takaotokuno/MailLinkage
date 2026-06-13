namespace MailBatch.Console.Options;

internal sealed class ImapOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 143;
    public bool UseSsl { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Mailbox { get; init; } = "INBOX";
}
