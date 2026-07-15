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
    /// 状態: テスト用の受信メール API を起動する。
    /// 振る舞い: ヘルスチェックが成功し Healthy 状態を返す。
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
    /// 状態: 有効な受信メール作成リクエストを同じ MessageId で 2 回送信する。
    /// 振る舞い: 初回は保存され、重複する 2 回目は競合として拒否される。
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
    /// 状態: 前後空白を含む受信メール作成リクエストを送信する。
    /// 振る舞い: 値がトリムされ、作成されたリソースを ID で取得できる。
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
    /// 状態: 本文が空文字の受信メール作成リクエストを送信する。
    /// 振る舞い: 保存結果の本文が null に正規化される。
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
    /// 状態: 存在しない受信メール ID を指定する。
    /// 振る舞い: 取得 API が NotFound を返す。
    /// </summary>
    [Fact]
    public async Task GetReceivedMailById_ReturnsNotFoundForUnknownId()
    {
        using HttpClient client = CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/received-mails/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 状態: 必須項目や形式が不正な受信メール作成リクエストを送信する。
    /// 振る舞い: BadRequest と検証エラー詳細が返される。
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
