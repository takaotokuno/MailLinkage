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
    AppOptions options,
    IMailFolderProvider mailFolderProvider,
    ILogger<MailKitSearcher> logger) : IMailKitSearcher
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
