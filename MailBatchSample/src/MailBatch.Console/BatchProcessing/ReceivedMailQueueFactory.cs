using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal sealed class ReceivedMailQueueFactory : IReceivedMailQueueFactory
{
    public Channel<ReceivedMailRequest> Create()
    {
        return Channel.CreateUnbounded<ReceivedMailRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }
}
