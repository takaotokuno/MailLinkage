using System.Threading.Channels;
using MailBatch.Console.Infrastructure;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

internal sealed class ReceivedMailPipelineComponentFactory(
    AppOptions options,
    IReceivedMailFolderService receivedMailFolderService,
    IApiClient apiClient,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> producerLogger,
    ILogger<ApiQueueConsumer> consumerLogger) : IReceivedMailPipelineComponentFactory
{
    public IMailFetchQueueProducer CreateProducer(ChannelWriter<ReceivedMailRequest> writer)
    {
        return new MailFetchQueueProducer(
            receivedMailFolderService,
            writer,
            mailNotifier,
            mailNotificationFactory,
            producerLogger);
    }

    public IApiQueueConsumer CreateConsumer(ChannelReader<ReceivedMailRequest> reader)
    {
        return new ApiQueueConsumer(
            options,
            receivedMailFolderService,
            apiClient,
            reader,
            consumerLogger);
    }
}
