using System.Threading.Channels;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.Pipeline;

public sealed class ReceivedMailQueueFactoryTests
{
    /// <summary>
    /// 状態: リクエストキューの上限が1に設定されている。
    /// 振る舞い: 上限に達すると追加書き込みは待機扱いとなり、即時追加されない。
    /// </summary>
    [Fact]
    public void Create_UsesConfiguredBoundedCapacity()
    {
        ReceivedMailQueueFactory factory = new(new ProcessingOptions { RequestQueueCapacity = 1 });
        Channel<MailLinkageRequest> channel = factory.Create();

        Assert.True(channel.Writer.TryWrite(new MailLinkageRequest(new ReceivedMailId(1), "key-1", "message-1")));
        Assert.False(channel.Writer.TryWrite(new MailLinkageRequest(new ReceivedMailId(2), "key-2", "message-2")));
    }
}
