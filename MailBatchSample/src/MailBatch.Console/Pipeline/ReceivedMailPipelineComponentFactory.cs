using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

internal interface IReceivedMailPipelineComponentFactory
{
    IMailFetchQueueProducer CreateProducer(ChannelWriter<MailLinkageRequest> writer);

    IRequestQueueConsumer CreateConsumer(ChannelReader<MailLinkageRequest> reader);
}

internal sealed class ReceivedMailPipelineComponentFactory(
    ApiOptions apiOptions,
    IReceivedMailSession receivedMailSession,
    IApiClient apiClient,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> producerLogger,
    ILogger<MailLinkageRequest> consumerLogger) : IReceivedMailPipelineComponentFactory
{
    public IMailFetchQueueProducer CreateProducer(ChannelWriter<MailLinkageRequest> writer)
    {
        return new MailFetchQueueProducer(
            receivedMailSession,
            writer,
            mailNotifier,
            mailNotificationFactory,
            producerLogger);
    }

    public IRequestQueueConsumer CreateConsumer(ChannelReader<MailLinkageRequest> reader)
    {
        return new RequestQueueConsumer(
            apiOptions,
            receivedMailSession,
            apiClient,
            reader,
            consumerLogger);
    }
}
