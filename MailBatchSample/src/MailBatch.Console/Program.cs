using MailBatch.Console.Configuration;
using MailBatch.Console.Logging;
using MailBatch.Console.Services;
using Serilog;

int exitCode = 0;
string runId = Guid.NewGuid().ToString();

try
{
    MailBatch.Console.Options.AppOptions options = AppConfiguration.Load(args);
    Log.Logger = BatchLogger.Create(options.Batch.LogDirectory, runId);

    BatchRunner runner = new BatchRunner(options, runId);
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
