using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Fetching;

public sealed class MailItemExtractorTests
{
    /// <summary>
    /// 状態: 本文に Key 行が 1 件だけある場合に連携対象のキーを抽出できる。
    /// 振る舞い: 期待される結果を返す。
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
    /// 状態: 本文が空の場合に抽出エラーになる。
    /// 振る舞い: 期待される結果を返す。
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
    /// 状態: Key 行がない場合に抽出エラーになる。
    /// 振る舞い: 期待される結果を返す。
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
    /// 状態: Key 行が複数ある場合に抽出エラーになる。
    /// 振る舞い: 期待される結果を返す。
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
            MailId: new ReceivedMailId(123, 999),
            Sender: "sender@example.com",
            Subject: "subject",
            Body: body);
    }
}
