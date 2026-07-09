using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Infrastructure;

internal sealed class ReceivedMailSearcher(
    AppOptions options,
    IMailFolderProvider mailFolderProvider,
    ILogger<ReceivedMailSearcher> logger) : IReceivedMailSearcher
{
    public async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default)
    {
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        SearchQuery query = MailKitSearchQueryMapper.ToSearchQuery(condition);
        IList<UniqueId> uids = await folder.SearchAsync(query, cancellationToken);
        List<ReceivedMailId> targetMailIds = uids.Take(maxMessages).Select(MailKitReceivedMailIdMapper.ToReceivedMailId).ToList();

        logger.LogInformation(
            "Found {MessageCount} target messages. Mailbox={Mailbox}",
            targetMailIds.Count,
            options.Imap.Mailbox);

        return targetMailIds;
    }
}
