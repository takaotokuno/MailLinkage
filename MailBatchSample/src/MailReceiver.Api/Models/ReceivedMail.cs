namespace MailReceiver.Api.Models;

public sealed class ReceivedMail
{
    public const int MESSAGE_ID_MAX_LENGTH = 255;
    public const int SENDER_MAX_LENGTH = 320;
    public const int SUBJECT_MAX_LENGTH = 500;

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
