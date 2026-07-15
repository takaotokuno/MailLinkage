using System.Net.Http.Json;
using MailBatch.Console.Options;

namespace MailBatch.Console.Api;

internal interface IApiClient
{
    Task<HttpResponseMessage> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

internal sealed class ApiClient(HttpClient httpClient, AppOptions options) : IApiClient
{
    public Task<HttpResponseMessage> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default) => httpClient.PostAsJsonAsync(options.Api.Endpoint, request, cancellationToken);
}
