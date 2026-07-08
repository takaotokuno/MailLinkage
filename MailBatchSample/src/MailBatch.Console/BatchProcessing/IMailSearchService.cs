using MailKit;

namespace MailBatch.Console.BatchProcessing;

internal interface IMailSearchService
{
    Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(CancellationToken cancellationToken = default);
}
