namespace MailBatch.Console.Models;

internal sealed record ReceivedMailRequest(
    string MessageId,
    string Sender,
    string Subject,
    string? Body,
    DateTimeOffset ReceivedAt);
