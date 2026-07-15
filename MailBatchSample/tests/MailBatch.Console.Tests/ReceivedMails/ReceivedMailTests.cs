using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails;

public sealed class ReceivedMailTests
{
    // 件名と本文が上限以内の場合に検証エラーにならないことを確認する。
    [Fact]
    public void Validate_DoesNotThrowWhenSubjectAndBodyAreWithinLimits()
    {
        ReceivedMail mail = CreateMail(
            subject: new string('s', ReceivedMail.MaxSubjectLength),
            body: new string('b', ReceivedMail.MaxBodyLength));

        Exception? exception = Record.Exception(mail.Validate);

        Assert.Null(exception);
    }

    // 件名と本文が上限を超える場合に、それぞれのエラーメッセージが返ることを確認する。
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
