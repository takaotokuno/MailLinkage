using MailKit;
using MailKit.Search;

namespace MailBatch.Console.Infrastructure;

internal sealed class ReceivedMailSearcher : IReceivedMailSearcher
{
    public async Task<IReadOnlyList<UniqueId>> SearchAsync(IMailFolder receiveFolder, SearchQuery query, int maxMessages, CancellationToken cancellationToken = default)
    {
        IList<UniqueId> uids = await receiveFolder.SearchAsync(query, cancellationToken);
        return uids.Take(maxMessages).ToList();
    }
}
