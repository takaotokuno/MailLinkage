using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Infrastructure;

internal interface IProcessedMailMover
{
    Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, string messageId, CancellationToken cancellationToken = default);
}
