using System.Threading.Channels;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal interface IReceivedMailPipelineComponentFactory
{
    IMailFetchQueueProducer CreateProducer(ChannelWriter<ReceivedMailRequest> writer);

    IApiQueueConsumer CreateConsumer(ChannelReader<ReceivedMailRequest> reader);
}
