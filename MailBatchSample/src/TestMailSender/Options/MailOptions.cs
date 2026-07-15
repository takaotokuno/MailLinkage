namespace TestMailSender.Options;

internal sealed class MailOptions
{
    public string Mode { get; init; } = "target";
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string TargetSubject { get; init; } = string.Empty;
    public string NonTargetSubject { get; init; } = string.Empty;
    public string? Subject
    {
        get; init;
    }
    public string Body { get; init; } = string.Empty;
    public string DuplicateMessageId { get; init; } = string.Empty;
}
