using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal sealed class MailSearchService(
    AppOptions options,
    IReceivedMailFolderService receivedMailFolderService) : IMailSearchService
{
    public async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(CancellationToken cancellationToken = default)
    {
        MailSearchCondition condition = MailSearchConditionFactory.Create(options.MailSearch);
        return await receivedMailFolderService.SearchTargetMessagesAsync(condition, options.MailSearch.MaxMessages, cancellationToken);
    }
}
