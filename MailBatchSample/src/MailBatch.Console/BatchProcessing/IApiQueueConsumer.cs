using System.Threading.Channels;
using MailBatch.Console.Models;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal interface IApiQueueConsumer
{
    Task<ProcessResult> ConsumeAsync(ChannelReader<ReceivedMailRequest> reader);
}
