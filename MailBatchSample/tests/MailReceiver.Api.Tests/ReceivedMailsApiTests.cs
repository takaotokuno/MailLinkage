using System.Net;
using System.Net.Http.Json;
using MailReceiver.Api.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MailReceiver.Api.Tests;

public sealed class ReceivedMailsApiTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"mail-receiver-tests-{Guid.NewGuid():N}.db");
    private WebApplicationFactory<Program>? _factory;

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                _ = builder.ConfigureAppConfiguration((context, configuration) =>
                {
                    _ = configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:MailReceiver"] = $"Data Source={_databasePath}"
                    });
                });
            });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    /// <summary>
    /// 状態: ヘルスチェックエンドポイントが正常応答し、状態として Healthy を返す。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health");

        _ = response.EnsureSuccessStatusCode();
        Dictionary<string, string>? content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", content?["status"]);
    }

    /// <summary>
    /// 状態: 受信メール作成 API がメールを保存し、同じ MessageId の重複登録を拒否する。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task CreateReceivedMail_PersistsMailAndRejectsDuplicateMessageId()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(
            MessageId: "<api-test-message@example.com>",
            Sender: "sender@example.com",
            Subject: "API test subject",
            Body: "API test body",
            ReceivedAt: "2026-06-24T12:00:00Z");

        using HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        List<ReceivedMailResponse>? mails = await client.GetFromJsonAsync<List<ReceivedMailResponse>>("/api/received-mails");

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        ReceivedMailResponse mail = Assert.Single(mails ?? []);
        Assert.Equal(request.MessageId, mail.MessageId);
        Assert.Equal(request.Sender, mail.Sender);
        Assert.Equal(request.Subject, mail.Subject);
        Assert.Equal(request.Body, mail.Body);
    }

    /// <summary>
    /// 状態: 受信メール作成 API が入力値の前後空白を除去し、作成したリソースを ID で取得できる。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task CreateReceivedMail_TrimsValuesAndReturnsCreatedResourceById()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(
            MessageId: "  <trimmed-message@example.com>  ",
            Sender: "  sender@example.com  ",
            Subject: "  Trimmed subject  ",
            Body: null,
            ReceivedAt: "  2026-06-24T12:00:00+09:00  ");

        using HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        ReceivedMailResponse? createdMail = await createdResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();
        using HttpResponseMessage fetchedResponse = await client.GetAsync($"/api/received-mails/{createdMail?.Id}");
        ReceivedMailResponse? fetchedMail = await fetchedResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(createdResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetchedResponse.StatusCode);
        Assert.Equal("<trimmed-message@example.com>", fetchedMail?.MessageId);
        Assert.Equal("sender@example.com", fetchedMail?.Sender);
        Assert.Equal("Trimmed subject", fetchedMail?.Subject);
        Assert.Null(fetchedMail?.Body);
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.FromHours(9)), fetchedMail?.ReceivedAt);
    }

    /// <summary>
    /// 状態: 空文字の本文が保存時に null として正規化される。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task CreateReceivedMail_NormalizesEmptyBodyToNull()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(
            MessageId: "<empty-body-message@example.com>",
            Sender: "sender@example.com",
            Subject: "Empty body",
            Body: string.Empty,
            ReceivedAt: "2026-06-24T12:00:00Z");

        using HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        ReceivedMailResponse? createdMail = await createdResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Null(createdMail?.Body);
    }

    /// <summary>
    /// 状態: 存在しない受信メール ID を取得しようとした場合に NotFound を返す。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task GetReceivedMailById_ReturnsNotFoundForUnknownId()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/received-mails/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 状態: 不正な受信メール作成リクエストに対して検証エラーの詳細を返す。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public async Task CreateReceivedMail_ReturnsValidationProblemForInvalidRequest()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(
            MessageId: "   ",
            Sender: "not-an-email",
            Subject: new string('s', 501),
            Body: "body",
            ReceivedAt: "not-a-date");

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/received-mails", request);
        HttpValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateReceivedMailRequest.MessageId), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.Sender), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.Subject), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.ReceivedAt), problem?.Errors.Keys ?? []);
    }

    private HttpClient CreateClient() => (_factory ?? throw new InvalidOperationException("Test factory was not initialized.")).CreateClient();
}
