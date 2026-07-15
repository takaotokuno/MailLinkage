using System.Net;
using System.Text;
using MailBatch.Console.Api;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiClientTests
{
    /// <summary>
    /// 状態: API クライアントに正常応答を返す HTTP ハンドラーを設定する。
    /// 振る舞い: 設定済みエンドポイントへ POST し、成功結果を返す。
    /// </summary>
    [Fact]
    public async Task PostReceivedMailAsync_PostsConfiguredEndpointAndReturnsSuccessResult()
    {
        using StubHttpMessageHandler handler = new(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("created")
            };
        });
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.example.local")
        };
        ApiClient client = new(httpClient, new ApiOptions { Endpoint = "/received-mails" });

        ApiPostResult result = await client.PostReceivedMailAsync(new ApiRequest("linked message"));

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("created", result.ResponseBody);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal(new Uri("https://api.example.local/received-mails"), handler.Request?.RequestUri);
        Assert.Equal(/*lang=json,strict*/ "{\"Message\":\"linked message\"}", handler.RequestBody);
    }

    /// <summary>
    /// 状態: API クライアントに失敗応答と本文を返す HTTP ハンドラーを設定する。
    /// 振る舞い: 失敗結果にステータスコードと応答本文を含める。
    /// </summary>
    [Fact]
    public async Task PostReceivedMailAsync_ReturnsFailureResultWithResponseBody()
    {
        using StubHttpMessageHandler handler = new(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("invalid request")
            };
        });
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.example.local")
        };
        ApiClient client = new(httpClient, new ApiOptions { Endpoint = "/received-mails" });

        ApiPostResult result = await client.PostReceivedMailAsync(new ApiRequest("linked message"));

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("invalid request", result.ResponseBody);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content is not null)
            {
                RequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return responder(request);
        }
    }
}
