namespace MailBatch.Console.Options;

internal sealed class AppOptions
{
    public BatchOptions Batch { get; init; } = new();
    public ImapOptions Imap { get; init; } = new();
    public MailSearchOptions MailSearch { get; init; } = new();
    public ApiOptions Api { get; init; } = new();
    public ProcessingOptions Processing { get; init; } = new();

    public void Validate()
    {
        Require(Batch.LogDirectory, "Batch:LogDirectory");
        Require(Imap.Host, "Imap:Host");
        Require(Imap.UserName, "Imap:UserName");
        Require(Imap.Password, "Imap:Password");
        Require(Imap.Mailbox, "Imap:Mailbox");
        if (Imap.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Imap:Port must be between 1 and 65535.");
        }

        if (Api.BaseUrl is null || !Api.BaseUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Api:BaseUrl must be an absolute URI.");
        }

        Require(Api.Endpoint, "Api:Endpoint");
        if (Api.TimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Api:TimeoutSeconds must be greater than 0.");
        }

        if (MailSearch.MaxMessages <= 0)
        {
            throw new InvalidOperationException("MailSearch:MaxMessages must be greater than 0.");
        }
    }

    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }
}
