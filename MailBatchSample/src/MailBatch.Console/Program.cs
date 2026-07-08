using MailBatch.Console.Configuration;
using MailBatch.Console.Logging;
using MailBatch.Console.Options;
using MailBatch.Console.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

int exitCode = 0;
string runId = Guid.NewGuid().ToString();

try
{
    AppConfiguration.LoadedConfiguration loadedConfiguration = AppConfiguration.Load(args);
    AppOptions options = loadedConfiguration.Options;
    Log.Logger = BatchLogger.Create(loadedConfiguration.Configuration, runId);

    await using ServiceProvider serviceProvider = new ServiceCollection()
        .AddSingleton(options)
        .AddSingleton(new BatchRunContext(runId))
        .AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false))
        .AddTransient<BatchRunner>()
        .BuildServiceProvider();

    BatchRunner runner = serviceProvider.GetRequiredService<BatchRunner>();
    exitCode = await runner.RunAsync();
}
catch (Exception ex)
{
    exitCode = 1;
    if (Log.Logger.GetType().Name == "SilentLogger")
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
    }

    Log.Fatal(ex, "Mail batch failed with an unhandled exception. RunId={RunId}", runId);
}
finally
{
    await Log.CloseAndFlushAsync();
}

return exitCode;
