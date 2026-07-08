using System.Net.Http.Json;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;

namespace MailBatch.Console.Infrastructure;

internal sealed class ReceivedMailApiClient(HttpClient httpClient, AppOptions options) : IApiClient
{
    public Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request)
    {
        return httpClient.PostAsJsonAsync(options.Api.Endpoint, request);
    }
}
