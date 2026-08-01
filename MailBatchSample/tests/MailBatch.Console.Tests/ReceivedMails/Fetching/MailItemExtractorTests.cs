using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Fetching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Fetching;

public sealed class MailItemExtractorTests
{

    // 目的: 本文の単一Key行から連携項目を抽出できることを確認する。
    // 前提・入力: 本文中に「Key: ABC123」を1行だけ含むメールを渡す。
    // 期待結果: 抽出結果のKeyが「ABC123」となり、元メールの識別情報が維持される。
    // 検知したい異常: Key値の切り出し失敗またはメール識別情報の欠落。
    [Fact]
    public void Extract_ReturnsMailItemWhenBodyContainsSingleKeyLine()
    {
        ReceivedMail mail = CreateMail("Hello\nKey: ABC123\nRegards");

        ExtractedMailItem item = MailItemExtractor.Extract(mail);

        Assert.Equal(mail.MailId, item.MailId);
        Assert.Equal("ABC123", item.Key);
    }

    // 目的: 空本文を抽出不能として拒否することを確認する。
    // 前提・入力: 本文が空文字列のメールを渡す。
    // 期待結果: 本文が空であることを示すMailExtractionExceptionが送出される。
    // 検知したい異常: 空本文を正常データとして連携する不具合。
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

    // 目的: Key行のない本文を抽出不能として拒否することを確認する。
    // 前提・入力: 通常文だけでKey行を含まないメールを渡す。
    // 期待結果: Key行が見つからないことを示すMailExtractionExceptionが送出される。
    // 検知したい異常: 連携キーなしのメールを正常データとして扱う不具合。
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

    // 目的: 複数Key行を曖昧な入力として拒否することを確認する。
    // 前提・入力: 異なるKey値の行を2件含むメールを渡す。
    // 期待結果: Key行が複数あることを示すMailExtractionExceptionが送出される。
    // 検知したい異常: 複数候補から任意のKeyを選んで誤連携する不具合。
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
            To: "recipient@example.com",
            Subject: "subject",
            Body: body);
    }
}
