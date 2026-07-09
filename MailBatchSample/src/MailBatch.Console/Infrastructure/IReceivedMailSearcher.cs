using MailBatch.Console.Models;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Infrastructure;

internal interface IReceivedMailSearcher
{
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default);
}
