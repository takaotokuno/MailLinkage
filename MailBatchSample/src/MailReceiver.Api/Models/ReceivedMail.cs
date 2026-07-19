namespace MailReceiver.Api.Models;

public sealed class ReceivedMail
{
    public const int KEY_MAX_LENGTH = 255;
    public const int MESSAGE_MAX_LENGTH = 500;

    public long Id
    {
        get; set;
    }

    public required string Key
    {
        get; set;
    }

    public required string Message
    {
        get; set;
    }

    public DateTimeOffset CreatedAt
    {
        get; set;
    }
}
