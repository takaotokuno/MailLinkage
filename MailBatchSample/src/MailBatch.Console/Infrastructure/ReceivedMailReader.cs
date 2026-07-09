using MailBatch.Console.ReceivedMails;
using MailKit;
using MimeKit;

namespace MailBatch.Console.Infrastructure;

internal sealed class ReceivedMailReader : IReceivedMailReader
{
    public async Task<ReceivedMailContent> ReadAsync(IMailFolder receiveFolder, UniqueId uid, CancellationToken cancellationToken = default)
    {
        MimeMessage message = await receiveFolder.GetMessageAsync(uid, cancellationToken);
        IList<IMessageSummary> summary = await receiveFolder.FetchAsync([uid], MessageSummaryItems.InternalDate, cancellationToken);
        return new ReceivedMailContent(uid, message, summary.FirstOrDefault()?.InternalDate);
    }
}
