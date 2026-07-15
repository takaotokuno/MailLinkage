namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// メールから抽出したキー情報を格納する
/// </summary>
internal readonly record struct ExtractedMailItem(ReceivedMailId MailId, string Key);
