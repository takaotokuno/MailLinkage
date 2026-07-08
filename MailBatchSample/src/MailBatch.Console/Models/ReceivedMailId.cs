namespace MailBatch.Console.Models;

/// <summary>
/// 受信メールをアプリケーション層で識別するための値オブジェクトです。
/// </summary>
internal readonly record struct ReceivedMailId(uint Value)
{
    /// <summary>
    /// ログなどの表示用に識別子の文字列表現を返します。
    /// </summary>
    public override string ToString() => Value.ToString();
}
