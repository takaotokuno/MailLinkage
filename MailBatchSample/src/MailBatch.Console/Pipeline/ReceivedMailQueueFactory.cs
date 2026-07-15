using System.Threading.Channels;
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
internal sealed class ReceivedMailQueueFactory : IReceivedMailQueueFactory
{
    /// <summary>
    /// 単一の生産者と消費者で使用する非バウンドチャネルを作成します。
    /// </summary>
    public Channel<MailLinkageRequest> Create()
    {
        return Channel.CreateUnbounded<MailLinkageRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }
}
