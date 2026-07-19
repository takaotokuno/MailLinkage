using MailBatch.Console.ReceivedMails.Folders;
using MailKit;
using MimeKit;

namespace MailBatch.Console.ReceivedMails.MailKit;

/// <summary>
/// IMAPフォルダーからメール本文を読み取り、業務連携用の受信メールモデルへ変換します。
/// </summary>
internal interface IMailKitReader
{
    /// <summary>
    /// 指定された受信メールIDのメール内容を読み取ります。
    /// </summary>
    Task<ReceivedMail> ReadAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 指定されたIMAPメールを読み取り、形式不正を業務エラーへ変換します。
/// </summary>
internal sealed class MailKitReader(IMailFolderProvider mailFolderProvider) : IMailKitReader
{
    /// <summary>
    /// 指定された受信メールIDのメール内容を読み取ります。
    /// </summary>
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
        // 破損メールは業務データ不備として扱い、バッチ全体の予期せぬ異常終了と区別します。
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
            message.From.ToString(),
            message.Subject ?? "",
            message.TextBody ?? "");
    }
}
