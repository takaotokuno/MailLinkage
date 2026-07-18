using System.Threading.Channels;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Pipeline;

/// <summary>
/// 受信メール連携リクエスト用の内部キューを作成します。
/// </summary>
internal interface IReceivedMailQueueFactory
{
    /// <summary>
    /// 生産者と消費者を接続するチャネルを作成します。
    /// </summary>
    Channel<MailLinkageRequest> Create();
}

/// <summary>
/// 受信メール連携リクエスト用のチャネルを作成します。
/// </summary>
internal sealed class ReceivedMailQueueFactory(ProcessingOptions options) : IReceivedMailQueueFactory
{
    /// <summary>
    /// 単一の生産者と消費者で使用するバウンドチャネルを作成します。
    /// </summary>
    public Channel<MailLinkageRequest> Create()
    {
        return Channel.CreateBounded<MailLinkageRequest>(new BoundedChannelOptions(options.RequestQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
    }
}
