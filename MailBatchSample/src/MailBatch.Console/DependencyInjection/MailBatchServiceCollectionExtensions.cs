using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.BatchProcessing.Locking;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Imap;
using MailBatch.Console.ReceivedMails.MailKit;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.ReceivedMails.Recovery;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MailBatch.Console.DependencyInjection;

/// <summary>
/// MailBatch.Consoleで使用する依存関係を登録します。
/// </summary>
internal static class BatchServiceCollectionExtensions
{
    /// <summary>
    /// バッチアプリケーションで使用するサービスをDIコンテナへ登録します。
    /// </summary>
    public static IServiceCollection AddBatchApplication(
        this IServiceCollection services,
        AppOptions options,
        string runId,
        Serilog.ILogger logger)
    {
        _ = services
            .AddMailBatchOptions(options)
            .AddSingleton(new BatchRunContext(runId))
            .AddLogging(builder =>
            {
                _ = builder.ClearProviders();
                _ = builder.AddSerilog(logger, dispose: false);
            })
            .AddNotificationServices()
            .AddReceivedMailServices()
            .AddPipelineServices()
            .AddRunStatusServices()
            .AddApiClient(options.Api)
            .AddSingleton<IJobExecutionLock, FileJobExecutionLock>()
            .AddTransient<BatchRunner>();

        return services;
    }

    /// <summary>
    /// メールバッチの設定オブジェクトをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddMailBatchOptions(this IServiceCollection services, AppOptions options)
    {
        return services
            .AddSingleton(options.Batch)
            .AddSingleton(options.Imap)
            .AddSingleton(options.MailSearch)
            .AddSingleton(options.Api)
            .AddSingleton(options.Processing)
            .AddSingleton(options.Notification);
    }

    /// <summary>
    /// 通知メール関連サービスをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        return services
            .AddTransient<IMailNotifier, SmtpMailNotifier>()
            .AddTransient<MailNotificationFactory>()
            .AddTransient<IMetricAlertNotifier, MetricAlertNotifier>()
            .AddTransient<IStateMetricAlertMonitor, StateMetricAlertMonitor>()
            .AddTransient<IHistoricalMetricAlertMonitor, HistoricalMetricAlertMonitor>()
            .AddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// 受信メール処理関連サービスをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddReceivedMailServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IImapConnection, ImapConnection>()
            .AddScoped<IMailFolderProvider, MailFolderProvider>()
            .AddScoped<IMailKitSearcher, MailKitSearcher>()
            .AddScoped<IMailKitReader, MailKitReader>()
            .AddScoped<IMailKitMailMover, MailKitMailMover>()
            .AddScoped<ReceivedMailSession>()
            .AddScoped<IReceivedMailSession>(provider => provider.GetRequiredService<ReceivedMailSession>())
            .AddScoped<IReceivedMailSearcher>(provider => provider.GetRequiredService<ReceivedMailSession>())
            .AddScoped<IReceivedMailMover>(provider => provider.GetRequiredService<ReceivedMailSession>())
            .AddScoped<IMailMoveFailureRecoveryService, MailMoveFailureRecoveryService>();
    }

    /// <summary>
    /// 受信メールパイプライン関連サービスをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<SqliteMailProcessingStore>()
            .AddSingleton<IProcessedMailStore>(provider => provider.GetRequiredService<SqliteMailProcessingStore>())
            .AddSingleton<IMailMoveFailureStore>(provider => provider.GetRequiredService<SqliteMailProcessingStore>())
            .AddSingleton<IBatchRunHistoryStore, SqliteBatchRunHistoryStore>()
            .AddSingleton<IApiExecutionResultStore, SqliteApiExecutionResultStore>()
            .AddTransient<IReceivedMailQueueFactory, ReceivedMailQueueFactory>()
            .AddTransient<IReceivedMailPipelineComponentFactory, ReceivedMailPipelineComponentFactory>()
            .AddTransient<IReceivedMailPipeline, ReceivedMailPipeline>();
    }

    /// <summary>
    /// 実行結果通知サービスをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddRunStatusServices(this IServiceCollection services) => services
        .AddTransient<IRunStatusNotifier, RunStatusNotifier>()
        .AddTransient<IBatchRunCompletionService, BatchRunCompletionService>();

    /// <summary>
    /// APIクライアントとリトライポリシーをDIコンテナへ登録します。
    /// </summary>
    private static IServiceCollection AddApiClient(this IServiceCollection services, ApiOptions apiOptions)
    {
        _ = services
            .AddHttpClient<IApiClient, ApiClient>(client =>
            {
                client.BaseAddress = apiOptions.BaseUrl;
                client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
            })
            .AddPolicyHandler(ApiRetryPolicyFactory.Create(apiOptions));

        return services;
    }
}
