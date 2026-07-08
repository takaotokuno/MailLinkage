using System.Net.Http.Json;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;

namespace MailBatch.Console.Infrastructure;

internal interface IReceivedMailApiClient
{
    Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ReceivedMailApiClient(HttpClient httpClient, AppOptions options) : IReceivedMailApiClient
{
    public Task<HttpResponseMessage> PostReceivedMailAsync(ReceivedMailRequest request, CancellationToken cancellationToken = default)
    {
        return httpClient.PostAsJsonAsync(options.Api.Endpoint, request, cancellationToken);
    }
}
