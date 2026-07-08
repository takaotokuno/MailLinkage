using System.Net.Http.Json;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;

namespace MailBatch.Console.BatchProcessing;

internal sealed class ApiClient(
    AppOptions options,
    IHttpClientFactory httpClientFactory) : IApiClient
{
    public async Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request)
    {
        HttpClient httpClient = httpClientFactory.CreateClient("ReceivedMailApi");
        return await httpClient.PostAsJsonAsync(options.Api.Endpoint, request);
    }
}
