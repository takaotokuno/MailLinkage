using System.Threading.Channels;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 上限なしの単一 Reader / Writer キューを生成します。
/// </summary>
internal sealed class UnboundedQueueFactory<T> : IQueueFactory<T>
{
    public Channel<T> CreateSingleReaderSingleWriterQueue()
    {
        return Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }
}
