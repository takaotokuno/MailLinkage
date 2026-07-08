using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal interface IApiClient
{
    Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request);
}
