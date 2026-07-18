using System.Threading.Channels;
using MailBatch.Console.Api;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Pipeline;

/// <summary>
/// パイプライン内で使用するProducer/Consumerをキューに合わせて生成します。
/// </summary>
internal interface IReceivedMailPipelineComponentFactory
{
    /// <summary>
    /// 指定されたキューWriterを使用するProducerを作成します。
    /// </summary>
    IMailFetchQueueProducer CreateProducer(ChannelWriter<MailLinkageRequest> writer);

    /// <summary>
    /// 指定されたキューReaderを使用するConsumerを作成します。
    /// </summary>
    IRequestQueueConsumer CreateConsumer(ChannelReader<MailLinkageRequest> reader);
}

/// <summary>
/// パイプライン部品の生成を担当します。
/// </summary>
internal sealed class ReceivedMailPipelineComponentFactory(
    ApiOptions apiOptions,
    IReceivedMailSession receivedMailSession,
    IApiClient apiClient,
    IMailNotifier mailNotifier,
    MailNotificationFactory mailNotificationFactory,
    ILogger<MailFetchQueueProducer> producerLogger,
    ILogger<MailLinkageRequest> consumerLogger,
    IProcessedMailMoveFailureStore moveFailureStore) : IReceivedMailPipelineComponentFactory
{
    /// <summary>
    /// 指定されたキューWriterを使用するProducerを作成します。
    /// </summary>
    public IMailFetchQueueProducer CreateProducer(ChannelWriter<MailLinkageRequest> writer)
    {
        return new MailFetchQueueProducer(
            receivedMailSession,
            writer,
            mailNotifier,
            mailNotificationFactory,
            moveFailureStore,
            producerLogger);
    }

    /// <summary>
    /// 指定されたキューReaderを使用するConsumerを作成します。
    /// </summary>
    public IRequestQueueConsumer CreateConsumer(ChannelReader<MailLinkageRequest> reader)
    {
        return new RequestQueueConsumer(
            apiOptions,
            receivedMailSession,
            apiClient,
            reader,
            moveFailureStore,
            consumerLogger);
    }
}
