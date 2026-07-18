namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// 受信メールをアプリケーション層で識別するための値オブジェクトです。
/// </summary>
internal readonly record struct ReceivedMailId(uint Uid, uint UidValidity)
{
    public override string ToString() => $"{UidValidity}:{Uid}";
}
