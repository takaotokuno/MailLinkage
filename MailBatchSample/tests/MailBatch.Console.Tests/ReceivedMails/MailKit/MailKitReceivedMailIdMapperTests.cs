using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.MailKit;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.MailKit;

public sealed class MailKitReceivedMailIdMapperTests
{
    /// <summary>
    /// 状態: MailKit の UniqueId を指定する。
    /// 振る舞い: 同じ数値を持つ ReceivedMailId に変換する。
    /// </summary>
    [Fact]
    public void ToReceivedMailId_ConvertsFromUniqueId()
    {
        ReceivedMailId mailId = MailKitReceivedMailIdMapper.ToReceivedMailId(new UniqueId(123));

        Assert.Equal(new ReceivedMailId(123), mailId);
    }

    /// <summary>
    /// 状態: ReceivedMailId を指定する。
    /// 振る舞い: 同じ数値を持つ MailKit の UniqueId に変換する。
    /// </summary>
    [Fact]
    public void ToUniqueId_ConvertsToUniqueId()
    {
        UniqueId uniqueId = MailKitReceivedMailIdMapper.ToUniqueId(new ReceivedMailId(456));

        Assert.Equal(new UniqueId(456), uniqueId);
    }
}
