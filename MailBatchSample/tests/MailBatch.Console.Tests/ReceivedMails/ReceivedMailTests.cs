using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails;

public sealed class ReceivedMailTests
{

    /// <summary>
    /// 件名と本文の最大長境界を有効として扱うことを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: 件名と本文をそれぞれ定義済み最大長ちょうどで作成する。<br/>
    /// 期待結果: Validateを実行しても例外が送出されない。<br/>
    /// 検知したい異常: 最大長ちょうどの正常メールを長さ超過として拒否する不具合。
    /// </remarks>
    [Fact]
    public void Validate_DoesNotThrowWhenSubjectAndBodyAreWithinLimits()
    {
        ReceivedMail mail = CreateMail(
            subject: new string('s', ReceivedMail.MAX_SUBJECT_LENGTH),
            body: new string('b', ReceivedMail.MAX_BODY_LENGTH));

        Exception? exception = Record.Exception(mail.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// 件名と本文の長さ超過を同時に報告できることを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: 件名と本文をそれぞれ最大長より1文字長くして作成する。<br/>
    /// 期待結果: 検証例外が送出され、件名と本文それぞれの上限超過メッセージを含む。<br/>
    /// 検知したい異常: 長さ超過を見逃す、または一方のエラーだけを報告する不具合。
    /// </remarks>
    [Fact]
    public void Validate_ThrowsErrorMessagesWhenSubjectAndBodyExceedLimits()
    {
        ReceivedMail mail = CreateMail(
            subject: new string('s', ReceivedMail.MAX_SUBJECT_LENGTH + 1),
            body: new string('b', ReceivedMail.MAX_BODY_LENGTH + 1));

        ReceivedMailContentValidationException exception = Assert.Throws<ReceivedMailContentValidationException>(mail.Validate);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Subject length", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Body length", exception.Message, StringComparison.Ordinal);
    }

    private static ReceivedMail CreateMail(string subject, string body)
    {
        return new ReceivedMail(
            MailId: new ReceivedMailId(123, 999),
            To: "recipient@example.com",
            Subject: subject,
            Body: body);
    }
}
