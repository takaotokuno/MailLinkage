using System.Threading.Channels;
using MailBatch.Console.Models;
using MailBatch.Console.ReceivedMails;
using MailKit;

namespace MailBatch.Console.BatchProcessing;

internal interface IMailFetchQueueProducer
{
    Task<ProcessResult> ProduceAsync(IReadOnlyList<UniqueId> targetUids, ChannelWriter<ReceivedMailRequest> writer);
}
