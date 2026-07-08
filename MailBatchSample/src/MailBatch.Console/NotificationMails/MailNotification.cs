namespace MailBatch.Console.NotificationMails;

internal sealed record MailNotification(
    string To,
    string Subject,
    string Body);
