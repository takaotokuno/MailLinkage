namespace MailReceiver.Api.Contracts;

internal sealed record NormalizedCreateReceivedMailRequest(
    string Key,
    string Message);
