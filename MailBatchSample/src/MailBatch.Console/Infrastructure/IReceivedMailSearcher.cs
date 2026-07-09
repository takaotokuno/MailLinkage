using MailKit;
using MailKit.Search;

namespace MailBatch.Console.Infrastructure;

internal interface IReceivedMailSearcher
{
    Task<IReadOnlyList<UniqueId>> SearchAsync(IMailFolder receiveFolder, SearchQuery query, int maxMessages, CancellationToken cancellationToken = default);
}
