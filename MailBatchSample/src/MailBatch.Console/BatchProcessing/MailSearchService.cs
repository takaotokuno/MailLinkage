using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailKit;

namespace MailBatch.Console.BatchProcessing;

internal sealed class MailSearchService(
    AppOptions options,
    IReceivedMailFolderService receivedMailFolderService) : IMailSearchService
{
    public async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(CancellationToken cancellationToken = default)
    {
        MailKit.Search.SearchQuery query = MailSearchQueryFactory.Create(options.MailSearch);
        return await receivedMailFolderService.SearchTargetMessagesAsync(query, options.MailSearch.MaxMessages, cancellationToken);
    }
}
