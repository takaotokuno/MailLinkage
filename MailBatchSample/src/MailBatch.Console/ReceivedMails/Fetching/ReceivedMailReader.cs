using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.MailKit;
using MailKit;
using MimeKit;

namespace MailBatch.Console.ReceivedMails.Fetching;

internal interface IReceivedMailReader
{
    Task<ReceivedMail> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

internal sealed class ReceivedMailReader(IMailFolderProvider mailFolderProvider) : IReceivedMailReader
{
    public async Task<ReceivedMail> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        UniqueId uid = MailKitReceivedMailIdMapper.ToUniqueId(mailId);
        IMailFolder folder = mailFolderProvider.GetOpenedReceiveFolder();
        MimeMessage message;
        try
        {
            message = await folder.GetMessageAsync(uid, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ParseException ex)
        {
            throw new ReceivedMailFormatException("MIME message is damaged or invalid.", ex);
        }
        catch (InvalidDataException ex)
        {
            throw new ReceivedMailFormatException("MIME message is damaged or invalid.", ex);
        }

        return new ReceivedMail(
            mailId,
            message.Sender?.ToString() ?? "",
            message.Subject ?? "",
            message.Body?.ToString() ?? "");
    }
}
