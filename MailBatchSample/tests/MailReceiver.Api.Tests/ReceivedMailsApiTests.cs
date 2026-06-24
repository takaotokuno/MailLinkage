using Xunit;
using System.Net;
using System.Net.Http.Json;
using MailReceiver.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

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
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
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

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", content?["status"]);
    }

    [Fact]
    public async Task CreateReceivedMail_PersistsMailAndRejectsDuplicateMessageId()
    {
        using var client = CreateClient();
        var request = new CreateReceivedMailRequest(
            MessageId: "<api-test-message@example.com>",
            Sender: "sender@example.com",
            Subject: "API test subject",
            Body: "API test body",
            ReceivedAt: "2026-06-24T12:00:00Z");

        using var createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        using var duplicateResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        var mails = await client.GetFromJsonAsync<List<ReceivedMailResponse>>("/api/received-mails");

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        var mail = Assert.Single(mails ?? []);
        Assert.Equal(request.MessageId, mail.MessageId);
        Assert.Equal(request.Sender, mail.Sender);
        Assert.Equal(request.Subject, mail.Subject);
        Assert.Equal(request.Body, mail.Body);
    }

    [Fact]
    public async Task CreateReceivedMail_TrimsValuesAndReturnsCreatedResourceById()
    {
        using var client = CreateClient();
        var request = new CreateReceivedMailRequest(
            MessageId: "  <trimmed-message@example.com>  ",
            Sender: "  sender@example.com  ",
            Subject: "  Trimmed subject  ",
            Body: null,
            ReceivedAt: "  2026-06-24T12:00:00+09:00  ");

        using var createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        var createdMail = await createdResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();
        using var fetchedResponse = await client.GetAsync($"/api/received-mails/{createdMail?.Id}");
        var fetchedMail = await fetchedResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(createdResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetchedResponse.StatusCode);
        Assert.Equal("<trimmed-message@example.com>", fetchedMail?.MessageId);
        Assert.Equal("sender@example.com", fetchedMail?.Sender);
        Assert.Equal("Trimmed subject", fetchedMail?.Subject);
        Assert.Null(fetchedMail?.Body);
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.FromHours(9)), fetchedMail?.ReceivedAt);
    }

    [Fact]
    public async Task GetReceivedMailById_ReturnsNotFoundForUnknownId()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/received-mails/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReceivedMail_ReturnsValidationProblemForInvalidRequest()
    {
        using var client = CreateClient();
        var request = new CreateReceivedMailRequest(
            MessageId: "   ",
            Sender: "not-an-email",
            Subject: new string('s', 501),
            Body: "body",
            ReceivedAt: "not-a-date");

        using var response = await client.PostAsJsonAsync("/api/received-mails", request);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateReceivedMailRequest.MessageId), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.Sender), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.Subject), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.ReceivedAt), problem?.Errors.Keys ?? []);
    }

    private HttpClient CreateClient() => (_factory ?? throw new InvalidOperationException("Test factory was not initialized.")).CreateClient();
}
