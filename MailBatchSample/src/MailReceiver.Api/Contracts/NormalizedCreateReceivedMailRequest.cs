namespace MailReceiver.Api.Contracts;

internal sealed record NormalizedCreateReceivedMailRequest(
    string MessageId,
    string Sender,
    string Subject,
    string? Body);
