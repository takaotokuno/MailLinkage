using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Imap;
using MailBatch.Console.ReceivedMails.MailKit;
using MailBatch.Console.ReceivedMails.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MailBatch.Console.DependencyInjection;

internal static class MailBatchServiceCollectionExtensions
{
    public static IServiceCollection AddMailBatchApplication(
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
            .AddTransient<BatchRunner>();

        return services;
    }

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

    private static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        return services
            .AddTransient<IMailNotifier, SmtpMailNotifier>()
            .AddTransient<MailNotificationFactory>();
    }

    private static IServiceCollection AddReceivedMailServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IImapConnection, ImapConnection>()
            .AddScoped<IMailFolderProvider, MailFolderProvider>()
            .AddScoped<IMailKitSearcher, MailKitSearcher>()
            .AddScoped<IReceivedMailReader, ReceivedMailReader>()
            .AddScoped<IProcessedMailMover, ProcessedMailMover>()
            .AddScoped<IReceivedMailSession, ReceivedMailSession>();
    }

    private static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        return services
            .AddTransient<IReceivedMailQueueFactory, ReceivedMailQueueFactory>()
            .AddTransient<IReceivedMailPipelineComponentFactory, ReceivedMailPipelineComponentFactory>()
            .AddTransient<IReceivedMailPipeline, ReceivedMailPipeline>();
    }

    private static IServiceCollection AddRunStatusServices(this IServiceCollection services) => services.AddTransient<IRunStatusNotifier, RunStatusNotifier>();

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
