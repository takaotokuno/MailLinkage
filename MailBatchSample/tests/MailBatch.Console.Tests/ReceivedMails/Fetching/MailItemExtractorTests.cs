using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Fetching;

public sealed class MailItemExtractorTests
{
    /// <summary>
    /// 状態: 本文にキー行が 1 件だけ含まれる受信メールを用意する。
    /// 振る舞い: キーを抽出したメール項目を返す。
    /// </summary>
    [Fact]
    public void Extract_ReturnsMailItemWhenBodyContainsSingleKeyLine()
    {
        ReceivedMail mail = CreateMail("Hello\nKey: ABC123\nRegards");

        ExtractedMailItem item = MailItemExtractor.Extract(mail);

        Assert.Equal(mail.MailId, item.MailId);
        Assert.Equal("ABC123", item.Key);
    }

    /// <summary>
    /// 状態: 本文が空の受信メールを用意する。
    /// 振る舞い: メール項目抽出で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Extract_ThrowsWhenBodyIsEmpty()
    {
        ReceivedMail mail = CreateMail(string.Empty);

        MailExtractionException exception = Assert.Throws<MailExtractionException>(() =>
        {
            return MailItemExtractor.Extract(mail);
        });

        Assert.Contains("Mail body must not be empty.", exception.Errors);
    }

    /// <summary>
    /// 状態: キー行を含まない本文の受信メールを用意する。
    /// 振る舞い: メール項目抽出で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Extract_ThrowsWhenKeyLineIsMissing()
    {
        ReceivedMail mail = CreateMail("Hello\nNo key here");

        MailExtractionException exception = Assert.Throws<MailExtractionException>(() =>
        {
            return MailItemExtractor.Extract(mail);
        });

        Assert.Contains("A key line", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 状態: キー行を複数含む本文の受信メールを用意する。
    /// 振る舞い: メール項目抽出で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void Extract_ThrowsWhenMultipleKeyLinesAreFound()
    {
        ReceivedMail mail = CreateMail("Key: ABC123\nKey: DEF456");

        MailExtractionException exception = Assert.Throws<MailExtractionException>(() =>
        {
            return MailItemExtractor.Extract(mail);
        });

        Assert.Contains("Multiple key lines", exception.Message, StringComparison.Ordinal);
    }

    private static ReceivedMail CreateMail(string body)
    {
        return new ReceivedMail(
            MailId: new ReceivedMailId(123),
            Sender: "sender@example.com",
            Subject: "subject",
            Body: body);
    }
}
