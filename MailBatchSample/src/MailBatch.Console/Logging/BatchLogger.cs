using Serilog;

namespace MailBatch.Console.Logging;

internal static class BatchLogger
{
    private const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({RunId}) {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// 指定されたログディレクトリと実行IDを使用して、コンソールとファイルに出力するロガーを作成します。
    /// </summary>
    public static ILogger Create(string logDirectory, string runId)
    {
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("RunId", runId)
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                Path.Combine(logDirectory, "batch-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }
}
