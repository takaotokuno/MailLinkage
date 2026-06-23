using MailBatch.Console.Configuration;
using MailBatch.Console.Logging;
using MailBatch.Console.Services;
using Serilog;

var exitCode = 0;
var runId = Guid.NewGuid().ToString("N");

try
{
    var options = AppConfiguration.Load(args);
    Log.Logger = BatchLogger.Create(options.Batch.LogDirectory, runId);

    var runner = new BatchRunner(options, runId);
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

// 変更テスト用