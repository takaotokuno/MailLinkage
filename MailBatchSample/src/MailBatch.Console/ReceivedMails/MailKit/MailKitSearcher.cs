using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Searching;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.MailKit;

/// <summary>
/// MailKitを使用して処理対象メールのIDを検索します。
/// </summary>
internal interface IMailKitSearcher
{
    /// <summary>
    /// 検索条件に一致する受信メールIDを検索します。
    /// </summary>
    Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default);
}

/// <summary>
/// MailKitを使用して処理対象メールを検索し、処理順を安定させます。
/// </summary>
internal sealed class MailKitSearcher(
    ImapOptions imapOptions,
    IMailFolderProvider mailFolderProvider,
    ILogger<MailKitSearcher> logger) : IMailKitSearcher
{
    /// <summary>
    /// 検索条件に一致する受信メールIDを検索します。
    /// </summary>
    public async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default)
    {
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        SearchQuery query = MailKitSearchQueryMapper.ToSearchQuery(condition);
        IList<UniqueId> uids = await folder.SearchAsync(query, cancellationToken);
        IList<IMessageSummary> summaries = await folder.FetchAsync(uids, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate, cancellationToken);
        // 受信日時とUIDで処理順を固定し、同じ検索条件でも再実行時の処理対象がぶれないようにします。
        List<ReceivedMailId> targetMailIds = summaries
            .OrderBy(summary => summary.InternalDate ?? DateTimeOffset.MaxValue)
            .ThenBy(summary => summary.UniqueId.Id)
            .Take(maxMessages)
            .Select(summary => MailKitReceivedMailIdMapper.ToReceivedMailId(summary.UniqueId, folder.UidValidity))
            .ToList();

        logger.LogInformation(
            "Found {MessageCount} target messages. Mailbox={Mailbox}",
            targetMailIds.Count,
            imapOptions.Mailbox);

        return targetMailIds;
    }
}
