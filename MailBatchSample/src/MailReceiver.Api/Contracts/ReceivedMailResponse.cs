namespace MailReceiver.Api.Contracts;

public sealed record ReceivedMailResponse(
    long Id,
    string MessageId,
    string Sender,
    string Subject,
    string? Body,
    DateTimeOffset ReceivedAt,
    DateTimeOffset CreatedAt);
