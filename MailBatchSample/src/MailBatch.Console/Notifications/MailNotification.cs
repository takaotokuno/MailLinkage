namespace MailBatch.Console.Notifications;

internal sealed record MailNotification(
    string To,
    string Subject,
    string Body);
