using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Configuration;
using MailBatch.Console.Infrastructure;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Models;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

int exitCode = 0;
string runId = Guid.NewGuid().ToString();

try
{
    AppConfiguration.LoadedConfiguration loadedConfiguration = AppConfiguration.Load(args);
    AppOptions options = loadedConfiguration.Options;

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(loadedConfiguration.Configuration)
        .Enrich.WithProperty("RunId", runId)
        .CreateLogger();

    await using ServiceProvider serviceProvider = new ServiceCollection()
        .AddSingleton(options)
        .AddSingleton(new BatchRunContext(runId))
        .AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, dispose: false);
        })
        .AddTransient<IMailNotifier, SmtpMailNotifier>()
        .AddTransient<MailNotificationFactory>()
        .AddTransient<IMailSearchService, MailSearchService>()
        .AddTransient<IReceivedMailPipeline, ReceivedMailPipeline>()
        .AddTransient<IExitCodePolicy, ExitCodePolicy>()
        .AddTransient<IRunStatusNotifier, RunStatusNotifier>()
        .AddTransient(typeof(IQueueFactory<>), typeof(UnboundedQueueFactory<>))
        .AddTransient<MailFetchQueueProducer>()
        .AddTransient<ApiQueueConsumer>()
        .AddTransient<MailFetchQueueProducerFactory>(sp => writer => ActivatorUtilities.CreateInstance<MailFetchQueueProducer>(sp, writer))
        .AddTransient<ApiQueueConsumerFactory>(sp => reader => ActivatorUtilities.CreateInstance<ApiQueueConsumer>(sp, reader))
        .AddScoped<IReceivedMailFolderService, ReceivedMailFolderService>()
        .AddHttpClient<IApiClient, ReceivedMailApiClient>(client =>
        {
            client.BaseAddress = options.Api.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.Api.TimeoutSeconds);
        })
        .AddPolicyHandler(CreateApiRetryPolicy(options.Api))
        .Services
        .AddTransient<BatchRunner>()
        .BuildServiceProvider();

    BatchRunner runner = serviceProvider.GetRequiredService<BatchRunner>();
    exitCode = await runner.RunAsync();
}
catch (Exception ex)
{
    exitCode = 1;

    Log.Fatal(
        ex,
        "Mail batch failed with an unhandled exception. RunId={RunId}",
        runId);
}
finally
{
    await Log.CloseAndFlushAsync();
}

return exitCode;


static IAsyncPolicy<HttpResponseMessage> CreateApiRetryPolicy(ApiOptions apiOptions)
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            apiOptions.RetryCount,
            retryAttempt => TimeSpan.FromSeconds(apiOptions.RetryDelaySeconds * Math.Pow(2, retryAttempt - 1)));
}
