using System.Net;
using System.Text.Json;
using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.Integration;

/// <summary>
/// メール取得からHTTP送信、メール移動、処理台帳への記録までの業務フローを検証します。
/// 外部IMAP/APIだけをテストダブルにし、プロダクションのパイプラインとSQLiteストアを結合します。
/// </summary>
public sealed class MailLinkageWorkflowTests : IDisposable
{
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), $"mail-batch-workflow-{Guid.NewGuid():N}");

    [Fact]
    public async Task ValidTargetMail_IsPostedOnceAndCannotBeLinkedAgain()
    {
        ReceivedMailId mailId = new(10, 20);
        RecordingMailSession mailSession = new(new ReceivedMail(mailId, "owner@example.com", "連携対象", "Key: ORDER-001\n商品を出荷してください"));
        RecordingApiHandler api = new(HttpStatusCode.Created, /*lang=json,strict*/ "{\"id\":42}");
        await using Workflow workflow = CreateWorkflow(mailSession, api);

        ProcessResult firstRun = await workflow.Pipeline.ProcessAsync([mailId]);
        ProcessResult secondRun = await workflow.Pipeline.ProcessAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, Succeeded: 1), firstRun);
        Assert.Equal(new ProcessResult(Total: 1), secondRun);
        Assert.Equal("ORDER-001", Assert.Single(api.Requests).Key);
        Assert.Equal("商品を出荷してください", api.Requests[0].Message);
        Assert.Equal("secret", api.ApiKeys.Single());
        Assert.Equal(mailId, mailSession.Processed.Single());
        Assert.True(await workflow.ProcessedStore.ContainsAsync(mailId));
    }

    [Fact]
    public async Task InvalidBusinessMail_IsNotSentAndIsRemovedFromTheInbox()
    {
        ReceivedMailId mailId = new(11, 20);
        RecordingMailSession mailSession = new(new ReceivedMail(mailId, "owner@example.com", "連携対象", "キーがありません"));
        RecordingApiHandler api = new(HttpStatusCode.Created, "{}");
        await using Workflow workflow = CreateWorkflow(mailSession, api);

        ProcessResult result = await workflow.Pipeline.ProcessAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, InvalidFormat: 1), result);
        Assert.Empty(api.Requests);
        Assert.Equal(mailId, mailSession.Processed.Single());
        Assert.Empty(mailSession.Errors);
        Assert.True(await workflow.ProcessedStore.ContainsAsync(mailId));
    }

    [Fact]
    public async Task RejectedApiRequest_IsMovedToErrorAndMayBeRetriedOnANewBatchRun()
    {
        ReceivedMailId mailId = new(12, 20);
        RecordingMailSession mailSession = new(new ReceivedMail(mailId, "owner@example.com", "連携対象", "Key: ORDER-002\n再送対象"));
        RecordingApiHandler api = new(HttpStatusCode.ServiceUnavailable, "temporarily unavailable");
        await using Workflow workflow = CreateWorkflow(mailSession, api);

        ProcessResult result = await workflow.Pipeline.ProcessAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, ApiFailed: 1), result);
        Assert.Single(api.Requests);
        Assert.Equal(mailId, mailSession.Errors.Single());
        Assert.Empty(mailSession.Processed);
        Assert.False(await workflow.ProcessedStore.ContainsAsync(mailId));
    }

    private Workflow CreateWorkflow(RecordingMailSession mailSession, RecordingApiHandler api)
    {
        BatchOptions batchOptions = new() { LogDirectory = _workDirectory };
        SqliteMailProcessingStore stateStore = new(batchOptions, NullLogger<SqliteMailProcessingStore>.Instance);
        SqliteApiExecutionResultStore resultStore = new(batchOptions, new BatchRunContext("integration-run"));
        ApiOptions apiOptions = new() { Endpoint = "/api/received-mails", ApiKey = "secret" };
        HttpClient httpClient = new(api) { BaseAddress = new Uri("http://api.test") };
        ApiClient apiClient = new(httpClient, apiOptions);
        MailNotificationFactory notificationFactory = new(CreateNotificationOptions(), new BatchRunContext("integration-run"));
        ReceivedMailPipelineComponentFactory components = new(
            apiOptions,
            mailSession,
            mailSession,
            apiClient,
            new NullMailNotifier(),
            notificationFactory,
            NullLogger<MailFetchQueueProducer>.Instance,
            NullLogger<MailLinkageRequest>.Instance,
            stateStore,
            stateStore,
            resultStore);
        ReceivedMailPipeline pipeline = new(
            new ReceivedMailQueueFactory(new ProcessingOptions { RequestQueueCapacity = 2 }),
            components,
            NullLogger<ReceivedMailPipeline>.Instance);
        return new Workflow(pipeline, stateStore, httpClient);
    }

    private static MailNotificationOptions CreateNotificationOptions() => new()
    {
        AdminAddress = "admin@example.com",
        Templates =
        [
            new() { Name = MailNotificationOptions.RUN_STATUS_TEMPLATE_NAME, Subject = "{Status}", Body = "{RunId}" },
            new() { Name = MailNotificationOptions.VALIDATION_ERROR_TEMPLATE_NAME, Subject = "入力エラー", Body = "{ValidationErrors}" },
            new() { Name = MailNotificationOptions.METRIC_ALERT_TEMPLATE_NAME, Subject = "{AlertTitle}", Body = "{AlertMessage}" }
        ]
    };

    public void Dispose()
    {
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
    }

    private sealed record Workflow(ReceivedMailPipeline Pipeline, IProcessedMailStore ProcessedStore, IDisposable Resource) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            Resource.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingApiHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public List<ApiRequest> Requests { get; } = [];
        public List<string> ApiKeys { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ApiRequest body = await JsonSerializer.DeserializeAsync<ApiRequest>(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken)
                ?? throw new InvalidOperationException("API request body was empty.");
            Requests.Add(body);
            ApiKeys.Add(request.Headers.GetValues("X-API-Key").Single());
            return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) };
        }
    }

    private sealed class RecordingMailSession(ReceivedMail mail) : IReceivedMailSession, IReceivedMailMover
    {
        public List<ReceivedMailId> Processed { get; } = [];
        public List<ReceivedMailId> Errors { get; } = [];
        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(mail);
        public Task<ReceivedMailId?> MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            Processed.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(mailId);
        }
        public Task<ReceivedMailId?> MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
        {
            Errors.Add(mailId);
            return Task.FromResult<ReceivedMailId?>(mailId);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NullMailNotifier : IMailNotifier
    {
        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
