using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails;

public sealed class ReceivedMailTests
{
    /// <summary>
    /// 状態: 件名と本文が上限内の受信メールを用意する。
    /// 振る舞い: 検証で例外を投げない。
    /// </summary>
    [Fact]
    public void Validate_DoesNotThrowWhenSubjectAndBodyAreWithinLimits()
    {
        ReceivedMail mail = CreateMail(
            subject: new string('s', ReceivedMail.MaxSubjectLength),
            body: new string('b', ReceivedMail.MaxBodyLength));

        Exception? exception = Record.Exception(mail.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// 状態: 件名と本文が上限を超える受信メールを用意する。
    /// 振る舞い: 検証で対象項目のエラーメッセージを投げる。
    /// </summary>
    [Fact]
    public void Validate_ThrowsErrorMessagesWhenSubjectAndBodyExceedLimits()
    {
        ReceivedMail mail = CreateMail(
            subject: new string('s', ReceivedMail.MaxSubjectLength + 1),
            body: new string('b', ReceivedMail.MaxBodyLength + 1));

        ReceivedMailContentValidationException exception = Assert.Throws<ReceivedMailContentValidationException>(mail.Validate);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Subject length", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Body length", exception.Message, StringComparison.Ordinal);
    }

    private static ReceivedMail CreateMail(string subject, string body)
    {
        return new ReceivedMail(
            MailId: new ReceivedMailId(123),
            Sender: "sender@example.com",
            Subject: subject,
            Body: body);
    }
}
