namespace MailBatch.Console.Options;

internal sealed class ApiOptions
{
    public Uri? BaseUrl { get; init; }
    public string Endpoint { get; init; } = "/api/received-mails";
    public int TimeoutSeconds { get; init; } = 30;
}
