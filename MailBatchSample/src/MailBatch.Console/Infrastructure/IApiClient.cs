using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.Infrastructure;

/// <summary>
/// 受信メール連携APIへアクセスするクライアントです。
/// </summary>
internal interface IApiClient
{
    Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request);
}
