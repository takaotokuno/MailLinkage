using MailBatch.Console.ReceivedMails;
using MailKit;
using MimeKit;

namespace MailBatch.Console.Infrastructure;

internal sealed class ReceivedMailReader(IMailFolderProvider mailFolderProvider) : IReceivedMailReader
{
    public async Task<ReceivedMailContent> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        UniqueId uid = MailKitReceivedMailIdMapper.ToUniqueId(mailId);
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);
        IList<IMessageSummary> summary = await folder.FetchAsync([uid], MessageSummaryItems.InternalDate, cancellationToken);

        return new ReceivedMailContent(message, summary.FirstOrDefault()?.InternalDate);
    }
}
