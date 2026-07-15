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
    Task<HttpResponseMessage> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// HttpClientを使用して受信メールAPIへリクエストを送信します。
/// </summary>
internal sealed class ApiClient(HttpClient httpClient, AppOptions options) : IApiClient
{
    /// <summary>
    /// 設定されたエンドポイントへ受信メールリクエストをPOSTします。
    /// </summary>
    public Task<HttpResponseMessage> PostReceivedMailAsync(ApiRequest request, CancellationToken cancellationToken = default) => httpClient.PostAsJsonAsync(options.Api.Endpoint, request, cancellationToken);
}
