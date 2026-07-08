using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal interface IReceivedMailQueueFactory
{
    Channel<ReceivedMailRequest> CreateQueue();
}
