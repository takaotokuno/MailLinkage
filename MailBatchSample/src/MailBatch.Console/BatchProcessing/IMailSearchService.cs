using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal interface IMailSearchService
{
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(CancellationToken cancellationToken = default);
}
