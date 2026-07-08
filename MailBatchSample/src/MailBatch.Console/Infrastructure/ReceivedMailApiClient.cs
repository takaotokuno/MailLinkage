using System.Net.Http.Json;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;

namespace MailBatch.Console.Infrastructure;

internal interface IApiClient
{
    Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ReceivedMailApiClient(HttpClient httpClient, AppOptions options) : IApiClient
{
    public Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsJsonAsync(options.Api.Endpoint, request, cancellationToken);
    }
}
