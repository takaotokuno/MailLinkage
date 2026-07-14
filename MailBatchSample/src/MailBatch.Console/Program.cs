using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Configuration;
using MailBatch.Console.NotificationMails;
using MailBatch.Console.Options;
using MailBatch.Console.Pipeline;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Imap;
using MailBatch.Console.ReceivedMails.MailKit;
using MailBatch.Console.ReceivedMails.Processing;
using MailBatch.Console.Service;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

int exitCode = 0;
string runId = Guid.NewGuid().ToString();
using CancellationTokenSource cancellationTokenSource = new();

// Ctrl + C を入力した場合の挙動を設定する
// DB接続等、外部接続が開いたままプログラムが終了することを防ぐため
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true; // デフォルトの処理（プログラムを即終了）をキャンセル
    cancellationTokenSource.Cancel(); // キャンセル要求を発行
};

try
{
    LoadedConfiguration loadedConfiguration = AppConfiguration.Load(args);
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
        .AddScoped<IImapConnection, ImapConnection>()
        .AddScoped<IMailFolderProvider, MailFolderProvider>()
        .AddScoped<IMailKitSearcher, MailKitSearcher>()
        .AddScoped<IReceivedMailReader, ReceivedMailReader>()
        .AddScoped<IProcessedMailMover, ProcessedMailMover>()
        .AddScoped<IReceivedMailMapper, ReceivedMailMapper>()
        .AddScoped<IReceivedMailSession, ReceivedMailSession>()
        .AddTransient<IReceivedMailQueueFactory, ReceivedMailQueueFactory>()
        .AddTransient<IReceivedMailPipelineComponentFactory, ReceivedMailPipelineComponentFactory>()
        .AddTransient<IReceivedMailPipeline, ReceivedMailPipeline>()
        .AddTransient<IRunStatusNotifier, RunStatusNotifier>()
        .AddHttpClient<IApiClient, ApiClient>(client =>
        {
            client.BaseAddress = options.Api.BaseUrl;
            client.Timeout = TimeSpan.FromSeconds(options.Api.TimeoutSeconds);
        })
        .AddPolicyHandler(CreateApiRetryPolicy(options.Api))
        .Services
        .AddTransient<BatchRunner>()
        .BuildServiceProvider();

    BatchRunner runner = serviceProvider.GetRequiredService<BatchRunner>();
    exitCode = await runner.RunAsync(cancellationTokenSource.Token);
}
catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
{
    exitCode = 130;

    Log.Warning(
        "Mail batch canceled. RunId={RunId}",
        runId);
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
    // メモリ上に残っているログを書き出してロガーを終了する
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
