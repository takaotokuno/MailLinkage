using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Processing;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace MailBatch.Console.Pipeline;

internal interface IReceivedMailPipelineComponentFactory
{
    IMailFetchQueueProducer CreateProducer(ChannelWriter<ApiRequest> writer);

    IApiQueueConsumer CreateConsumer(ChannelReader<ApiRequest> reader);
}

internal sealed class ReceivedMailPipelineComponentFactory(
    AppOptions options,
    IReceivedMailSession receivedMailSession,
    IApiClient apiClient,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> producerLogger,
    ILogger<ApiQueueConsumer> consumerLogger) : IReceivedMailPipelineComponentFactory
{
    public IMailFetchQueueProducer CreateProducer(ChannelWriter<ApiRequest> writer)
    {
        return new MailFetchQueueProducer(
            receivedMailSession,
            writer,
            mailNotifier,
            mailNotificationFactory,
            producerLogger);
    }

    public IApiQueueConsumer CreateConsumer(ChannelReader<ApiRequest> reader)
    {
        return new ApiQueueConsumer(
            options,
            receivedMailSession,
            apiClient,
            reader,
            consumerLogger);
    }
}
