using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.MailKit;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.MailKit;

public sealed class MailKitReceivedMailIdMapperTests
{
    /// <summary>
    /// 状態: MailKit の UniqueId をアプリケーション層の値オブジェクトへ変換する。
    /// 振る舞い: UID と UIDVALIDITY を保持した結果を返す。
    /// </summary>
    [Fact]
    public void ToReceivedMailId_ConvertsFromUniqueId()
    {
        ReceivedMailId mailId = MailKitReceivedMailIdMapper.ToReceivedMailId(new UniqueId(999, 123));

        Assert.Equal(new ReceivedMailId(123, 999), mailId);
    }

    /// <summary>
    /// 状態: MailKit の UniqueId に UIDVALIDITY が設定されていない。
    /// 振る舞い: 指定された UIDVALIDITY を補完した結果を返す。
    /// </summary>
    [Fact]
    public void ToReceivedMailId_UsesProvidedUidValidityWhenUniqueIdDoesNotHaveValidity()
    {
        ReceivedMailId mailId = MailKitReceivedMailIdMapper.ToReceivedMailId(new UniqueId(123), 999);

        Assert.Equal(new ReceivedMailId(123, 999), mailId);
    }

    /// <summary>
    /// 状態: アプリケーション層の値オブジェクトを MailKit の UniqueId へ戻せる。
    /// 振る舞い: UID と UIDVALIDITY を保持した結果を返す。
    /// </summary>
    [Fact]
    public void ToUniqueId_ConvertsToUniqueId()
    {
        UniqueId uniqueId = MailKitReceivedMailIdMapper.ToUniqueId(new ReceivedMailId(456, 999));

        Assert.Equal(456u, uniqueId.Id);
        Assert.Equal(999u, uniqueId.Validity);
    }
}
