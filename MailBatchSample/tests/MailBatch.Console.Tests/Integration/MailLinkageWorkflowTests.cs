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

    // 目的: 正常メールが一度だけAPI連携され再処理されないことを確認する。
    // 前提・入力: 有効なKey行を持つ未処理メールを受信箱に配置してバッチを2回実行する。
    // 期待結果: API送信は初回の1件のみで、メールは処理済みへ移動し、2回目には再送信されない。
    // 検知したい異常: 処理済みメールの重複連携または移動漏れ。
    [Fact]
    public async Task ValidTargetMail_IsPostedOnceAndCannotBeLinkedAgain()
    {
        ReceivedMailId mailId = new(10, 20);
        RecordingMailSession mailSession = new(new ReceivedMail(mailId, "owner@example.com", "連携対象", "Key: ORDER001\n商品を出荷してください"));
        RecordingApiHandler api = new(HttpStatusCode.Created, /*lang=json,strict*/ "{\"id\":42}");
        await using Workflow workflow = CreateWorkflow(mailSession, api);

        ProcessResult firstRun = await workflow.Pipeline.ProcessAsync([mailId]);
        ProcessResult secondRun = await workflow.Pipeline.ProcessAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, Succeeded: 1), firstRun);
        Assert.Equal(new ProcessResult(Total: 1), secondRun);
        Assert.Equal("ORDER001", Assert.Single(api.Requests).Key);
        Assert.Equal("連携対象\n\nKey: ORDER001\n商品を出荷してください", api.Requests[0].Message);
        Assert.Equal("secret", api.ApiKeys.Single());
        Assert.Equal(mailId, mailSession.Processed.Single());
        Assert.True(await workflow.ProcessedStore.ContainsAsync(mailId));
    }

    // 目的: 業務形式不正メールをAPIへ送らず受信箱から除外することを確認する。
    // 前提・入力: Key行を持たないメールを受信箱に配置する。
    // 期待結果: API送信は0件で、対象メールはエラーフォルダーへ移動する。
    // 検知したい異常: 不正メールの誤送信または受信箱への滞留。
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
        MailNotification notification = Assert.Single(workflow.Notifier.Notifications);
        Assert.Equal("owner@example.com", notification.To);
        Assert.Contains("A key line in the format 'Key: alphanumeric-value' was not found.", notification.Body);
    }

    // 目的: API拒否後のメールを次回実行で再試行できることを確認する。
    // 前提・入力: 初回は拒否し次回は成功するAPIと、有効な対象メールを用意する。
    // 期待結果: 初回はエラーへ移動し、次回は同じメールを再送信して処理済みへ移動する。
    // 検知したい異常: API拒否メールが再試行不能になる、または成功後もエラーに残る不具合。
    [Fact]
    public async Task RejectedApiRequest_IsMovedToErrorAndMayBeRetriedOnANewBatchRun()
    {
        ReceivedMailId mailId = new(12, 20);
        RecordingMailSession mailSession = new(new ReceivedMail(mailId, "owner@example.com", "連携対象", "Key: ORDER002\n再送対象"));
        RecordingApiHandler api = new(HttpStatusCode.ServiceUnavailable, "temporarily unavailable");
        await using Workflow workflow = CreateWorkflow(mailSession, api);

        ProcessResult firstRun = await workflow.Pipeline.ProcessAsync([mailId]);
        ProcessResult secondRun = await workflow.Pipeline.ProcessAsync([mailId]);

        Assert.Equal(new ProcessResult(Total: 1, ApiFailed: 1), firstRun);
        Assert.Equal(new ProcessResult(Total: 1, ApiFailed: 1), secondRun);
        Assert.Equal(2, api.Requests.Count);
        Assert.All(mailSession.Errors, movedMailId =>
        {
            Assert.Equal(mailId, movedMailId);
        });
        Assert.Equal(2, mailSession.Errors.Count);
        Assert.Empty(mailSession.Processed);
        Assert.False(await workflow.ProcessedStore.ContainsAsync(mailId));
    }

    private Workflow CreateWorkflow(RecordingMailSession mailSession, RecordingApiHandler api)
    {
        BatchOptions batchOptions = new()
        {
            LogDirectory = _workDirectory
        };
        SqliteMailProcessingStore stateStore = new(batchOptions, NullLogger<SqliteMailProcessingStore>.Instance);
        SqliteApiExecutionResultStore resultStore = new(batchOptions, new BatchRunContext("integration-run"));
        ApiOptions apiOptions = new()
        {
            Endpoint = "/api/received-mails",
            ApiKey = "secret"
        };
        HttpClient httpClient = new(api)
        {
            BaseAddress = new Uri("http://api.test")
        };
        ApiClient apiClient = new(httpClient, apiOptions);
        RecordingMailNotifier notifier = new();
        MailNotificationFactory notificationFactory = new(CreateNotificationOptions(), new BatchRunContext("integration-run"));
        ReceivedMailPipelineComponentFactory components = new(
            apiOptions,
            mailSession,
            mailSession,
            apiClient,
            notifier,
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
        return new Workflow(pipeline, stateStore, notifier, httpClient);
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

    private sealed record Workflow(
        ReceivedMailPipeline Pipeline,
        IProcessedMailStore ProcessedStore,
        RecordingMailNotifier Notifier,
        IDisposable Resource) : IAsyncDisposable
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

    private sealed class RecordingMailNotifier : IMailNotifier
    {
        public List<MailNotification> Notifications { get; } = [];

        public Task SendAsync(MailNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task SendAsync(IReadOnlyCollection<MailNotification> notifications, CancellationToken cancellationToken = default)
        {
            Notifications.AddRange(notifications);
            return Task.CompletedTask;
        }
    }
}
