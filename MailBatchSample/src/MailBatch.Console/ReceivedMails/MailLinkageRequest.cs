namespace MailBatch.Console.ReceivedMails;

internal readonly record struct MailLinkageRequest(
    ReceivedMailId MailId,
    string Key,
    string Message);
