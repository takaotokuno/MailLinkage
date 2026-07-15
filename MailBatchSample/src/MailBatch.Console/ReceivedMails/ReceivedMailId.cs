namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// 受信メールをアプリケーション層で識別するための値オブジェクトです。
/// </summary>
internal readonly record struct ReceivedMailId(uint Value)
{
    public override string ToString() => Value.ToString();
}
