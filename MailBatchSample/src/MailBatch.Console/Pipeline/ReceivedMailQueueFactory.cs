using MailBatch.Console.Api;
using System.Threading.Channels;

namespace MailBatch.Console.Service;

internal interface IReceivedMailQueueFactory
{
    Channel<ApiRequest> Create();
}

internal sealed class ReceivedMailQueueFactory : IReceivedMailQueueFactory
{
    public Channel<ApiRequest> Create()
    {
        return Channel.CreateUnbounded<ApiRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }
}
