namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// メールから抽出したAPI送信用データを表します。
/// </summary>
internal readonly record struct MailLinkageRequest(
    ReceivedMailId MailId,
    string Key,
    string Message);
