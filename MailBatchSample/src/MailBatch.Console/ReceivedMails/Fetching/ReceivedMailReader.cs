using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.MailKit;
using MailKit;
using MimeKit;

namespace MailBatch.Console.ReceivedMails.Fetching;

internal interface IReceivedMailReader
{
    Task<ReceivedMailContent> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

internal sealed class ReceivedMailReader(IMailFolderProvider mailFolderProvider) : IReceivedMailReader
{
    public async Task<ReceivedMailContent> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        UniqueId uid = MailKitReceivedMailIdMapper.ToUniqueId(mailId);
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);
        IList<IMessageSummary> summary = await folder.FetchAsync([uid], MessageSummaryItems.InternalDate, cancellationToken);

        return new ReceivedMailContent(
            MailKitReceivedMailIdMapper.ToReceivedMailId(uid),
            message.Sender?.ToString() ?? "",
            message.Subject ?? "",
            message.Body?.ToString() ?? "");
    }
}
