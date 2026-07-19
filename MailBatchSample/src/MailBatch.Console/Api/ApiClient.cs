using System.Net.Http.Json;
using MailBatch.Console.Options;

namespace MailBatch.Console.Api;

/// <summary>
/// 受信メールAPIへリクエストを送信するクライアント操作を提供します。
/// </summary>
internal interface IApiClient
{
    /// <summary>
    /// 受信メールリクエストをAPIへPOSTします。
    /// </summary>
    Task<ApiPostResult> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// HttpClientを使用して受信メールAPIへリクエストを送信します。
/// </summary>
internal sealed class ApiClient(HttpClient httpClient, ApiOptions apiOptions) : IApiClient
{
    private const string API_KEY_HEADER_NAME = "X-API-Key";

    /// <summary>
    /// 設定されたエンドポイントへ受信メールリクエストをPOSTします。
    /// </summary>
    public async Task<ApiPostResult> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, apiOptions.Endpoint)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add(API_KEY_HEADER_NAME, apiOptions.ApiKey);

        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ApiPostResult(response.IsSuccessStatusCode, (int)response.StatusCode, responseBody);
    }
}
