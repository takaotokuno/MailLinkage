namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// 受信メールと抽出済みキー情報からメール連携API用リクエストを生成します。
/// </summary>
internal static class MailLinkageRequestFactory
{
    public static MailLinkageRequest Create(ReceivedMail mail, ExtractedMailItem item)
    {
        return new MailLinkageRequest(
            item.MailId,
            item.Key,
            CreateMessage(mail.Subject, mail.Body));
    }

    private static string CreateMessage(string subject, string body)
    {
        int MAX_LENGTH = 500;
        string fullText = subject.Trim() + Environment.NewLine + Environment.NewLine + body.Trim();
        string summary = fullText[..Math.Min(MAX_LENGTH, fullText.Length)];

        return summary;
    }
}
