using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Searching;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.MailKit;

internal interface IMailKitSearcher
{
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default);
}

internal sealed class MailKitSearcher(
    ImapOptions imapOptions,
    IMailFolderProvider mailFolderProvider,
    ILogger<MailKitSearcher> logger) : IMailKitSearcher
{
    public async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default)
    {
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        SearchQuery query = MailKitSearchQueryMapper.ToSearchQuery(condition);
        IList<UniqueId> uids = await folder.SearchAsync(query, cancellationToken);
        IList<IMessageSummary> summaries = await folder.FetchAsync(uids, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, cancellationToken);
        List<ReceivedMailId> targetMailIds = summaries
            .OrderBy(summary => summary.InternalDate ?? DateTimeOffset.MaxValue)
            .ThenBy(summary => summary.UniqueId.Id)
            .Take(maxMessages)
            .Select(summary => MailKitReceivedMailIdMapper.ToReceivedMailId(summary.UniqueId))
            .ToList();

        logger.LogInformation(
            "Found {MessageCount} target messages. Mailbox={Mailbox}",
            targetMailIds.Count,
            imapOptions.Mailbox);

        return targetMailIds;
    }
}
