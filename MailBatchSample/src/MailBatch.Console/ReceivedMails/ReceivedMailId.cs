namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// 受信メールをアプリケーション層で識別するための値オブジェクトです。
/// </summary>
internal readonly record struct ReceivedMailId(uint Uid, uint UidValidity)
{
    /// <summary>
    /// 値をログ出力向けの文字列表現へ変換します。
    /// </summary>
    public override string ToString() => $"{UidValidity}:{Uid}";
}
