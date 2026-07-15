namespace MailReceiver.Api.Models;

public sealed class ReceivedMail
{
    public long Id
    {
        get; set;
    }

    public required string MessageId
    {
        get; set;
    }

    public required string Sender
    {
        get; set;
    }

    public required string Subject
    {
        get; set;
    }

    public string? Body
    {
        get; set;
    }

    public DateTimeOffset ReceivedAt
    {
        get; set;
    }

    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
