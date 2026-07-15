using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Pipeline;

internal interface IReceivedMailQueueFactory
{
    Channel<MailLinkageRequest> Create();
}

internal sealed class ReceivedMailQueueFactory : IReceivedMailQueueFactory
{
    public Channel<MailLinkageRequest> Create()
    {
        return Channel.CreateUnbounded<MailLinkageRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }
}
