namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// 受信メールと抽出済みキー情報からメール連携API用リクエストを生成します。
/// </summary>
internal static class MailLinkageRequestFactory
{
    private const int MAX_MESSAGE_LENGTH = 500;

    /// <summary>
    /// インスタンスまたは処理に必要な値を作成します。
    /// </summary>
    public static MailLinkageRequest Create(ReceivedMail mail, ExtractedMailItem item)
    {
        return new MailLinkageRequest(
            item.MailId,
            item.Key,
            CreateMessage(mail.Subject, mail.Body));
    }

    /// <summary>
    /// API連携用のメッセージ本文を作成します。
    /// </summary>
    private static string CreateMessage(string subject, string body)
    {
        string fullText = subject.Trim() + Environment.NewLine + Environment.NewLine + body.Trim();
        string summary = fullText[..Math.Min(MAX_MESSAGE_LENGTH, fullText.Length)];

        return summary;
    }
}
