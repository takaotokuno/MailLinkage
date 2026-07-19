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
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                                new Dictionary<string, string?> { ["ConnectionStrings:MailReceiver"] = $"Data Source={_databasePath}" });
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
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health");
        _ = response.EnsureSuccessStatusCode();
        Dictionary<string, string>? content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", content?["status"]);
    }

    [Fact]
    public async Task CreateReceivedMail_PersistsKeyAndMessageAndRejectsDuplicateKey()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(Key: "ABC123", Message: "API test message");

        using HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        List<ReceivedMailResponse>? mails = await client.GetFromJsonAsync<List<ReceivedMailResponse>>("/api/received-mails");

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        ReceivedMailResponse mail = Assert.Single(mails ?? []);
        Assert.Equal(request.Key, mail.Key);
        Assert.Equal(request.Message, mail.Message);
    }

    [Fact]
    public async Task CreateReceivedMail_TrimsValuesAndReturnsCreatedResourceById()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(Key: "  ABC123  ", Message: "  linked message  ");

        using HttpResponseMessage createdResponse = await client.PostAsJsonAsync("/api/received-mails", request);
        ReceivedMailResponse? createdMail = await createdResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();
        using HttpResponseMessage fetchedResponse = await client.GetAsync($"/api/received-mails/{createdMail?.Id}");
        ReceivedMailResponse? fetchedMail = await fetchedResponse.Content.ReadFromJsonAsync<ReceivedMailResponse>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(createdResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetchedResponse.StatusCode);
        Assert.Equal("ABC123", fetchedMail?.Key);
        Assert.Equal("linked message", fetchedMail?.Message);
    }

    [Fact]
    public async Task GetReceivedMailById_ReturnsNotFoundForUnknownId()
    {
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/received-mails/404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReceivedMail_ReturnsValidationProblemForInvalidRequest()
    {
        using HttpClient client = CreateClient();
        CreateReceivedMailRequest request = new(Key: "   ", Message: new string('m', 501));

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/received-mails", request);
        HttpValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateReceivedMailRequest.Key), problem?.Errors.Keys ?? []);
        Assert.Contains(nameof(CreateReceivedMailRequest.Message), problem?.Errors.Keys ?? []);
    }

    private HttpClient CreateClient() => (_factory ?? throw new InvalidOperationException("Test factory was not initialized.")).CreateClient();
}
