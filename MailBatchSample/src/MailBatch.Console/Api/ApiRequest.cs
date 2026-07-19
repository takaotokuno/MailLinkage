namespace MailBatch.Console.Api;

/// <summary>
/// 受信メールAPIへ送信するリクエスト本文を表します。
/// </summary>
internal sealed record ApiRequest(string Key, string Message);
