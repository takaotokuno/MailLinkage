namespace MailReceiver.Api.Contracts;

public sealed record CreateReceivedMailRequest(
    string? Key,
    string? Message);
