namespace MailReceiver.Api.Contracts;

public sealed record ReceivedMailResponse(
    long Id,
    string Key,
    string Message,
    DateTimeOffset CreatedAt);
