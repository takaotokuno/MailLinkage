using MailBatch.Console.Infrastructure;
using MailBatch.Console.ReceivedMails;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.Mail;

public sealed class MailKitReceivedMailIdMapperTests
{
    // MailKit の UniqueId をアプリケーション層の値オブジェクトへ変換することを確認する。
    [Fact]
    public void ToReceivedMailId_ConvertsFromUniqueId()
    {
        ReceivedMailId mailId = MailKitReceivedMailIdMapper.ToReceivedMailId(new UniqueId(123));

        Assert.Equal(new ReceivedMailId(123), mailId);
    }

    // アプリケーション層の値オブジェクトを MailKit の UniqueId へ戻せることを確認する。
    [Fact]
    public void ToUniqueId_ConvertsToUniqueId()
    {
        UniqueId uniqueId = MailKitReceivedMailIdMapper.ToUniqueId(new ReceivedMailId(456));

        Assert.Equal(new UniqueId(456), uniqueId);
    }
}
