namespace MailReceiver.Api.Contracts;

public sealed record CreateReceivedMailRequest(
    string? MessageId,
    string? Sender,
    string? Subject,
    string? Body,
    string? ReceivedAt);
