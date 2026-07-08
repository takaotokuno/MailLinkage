using MailBatch.Console.Mail;
using MailBatch.Console.Models;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.Mail;

public sealed class ReceivedMailIdMapperTests
{
    // MailKit の UniqueId をアプリケーション層の値オブジェクトへ変換することを確認する。
    [Fact]
    public void ToReceivedMailId_ConvertsFromUniqueId()
    {
        ReceivedMailId mailId = ReceivedMailIdMapper.ToReceivedMailId(new UniqueId(123));

        Assert.Equal(new ReceivedMailId(123), mailId);
    }

    // アプリケーション層の値オブジェクトを MailKit の UniqueId へ戻せることを確認する。
    [Fact]
    public void ToUniqueId_ConvertsToUniqueId()
    {
        UniqueId uniqueId = ReceivedMailIdMapper.ToUniqueId(new ReceivedMailId(456));

        Assert.Equal(new UniqueId(456), uniqueId);
    }
}
