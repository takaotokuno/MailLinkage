namespace MailBatch.Console.Options;

internal sealed class MailSearchOptions
{
    public string? SubjectContains { get; init; }
    public bool UnreadOnly { get; init; } = true;
    public int? SinceDays { get; init; } = 7;
    public int MaxMessages { get; init; } = 100;
}
