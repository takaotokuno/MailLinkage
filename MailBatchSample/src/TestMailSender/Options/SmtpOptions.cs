namespace TestMailSender.Options;

internal sealed class SmtpOptions
{
    private const int DEFAULT_PORT = 25;

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = DEFAULT_PORT;
    public string? UserName
    {
        get; init;
    }
    public string? Password
    {
        get; init;
    }
}
