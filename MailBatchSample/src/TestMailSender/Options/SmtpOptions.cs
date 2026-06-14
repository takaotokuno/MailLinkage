namespace TestMailSender.Options;

internal sealed class SmtpOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 25;
    public bool UseSsl { get; init; }
    public string? UserName { get; init; }
    public string? Password { get; init; }
}
