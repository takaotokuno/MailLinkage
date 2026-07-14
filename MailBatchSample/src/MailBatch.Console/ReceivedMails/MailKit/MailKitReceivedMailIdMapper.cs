using MailKit;

namespace MailBatch.Console.ReceivedMails.MailKit;

/// <summary>
/// MailKit固有のUIDとアプリケーション層の受信メールIDを相互変換します。
/// </summary>
internal static class MailKitReceivedMailIdMapper
{
    /// <summary>
    /// MailKitのUIDをアプリケーション層の受信メールIDへ変換します。
    /// </summary>
    public static ReceivedMailId ToReceivedMailId(UniqueId uid) => new(uid.Id);

    /// <summary>
    /// アプリケーション層の受信メールIDをMailKitのUIDへ変換します。
    /// </summary>
    public static UniqueId ToUniqueId(ReceivedMailId mailId) => new(mailId.Value);
}
