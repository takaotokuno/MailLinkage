using MailBatch.Console.Configuration;
using Serilog;

namespace MailBatch.Console.Logging;

/// <summary>
/// バッチ実行単位の識別子を付与したSerilogロガーを生成します。
/// </summary>
internal static class SerilogLoggerFactory
{
    public static Serilog.Core.Logger Create(LoadedConfiguration loadedConfiguration, string runId)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(loadedConfiguration.Configuration)
            .Enrich.WithProperty("RunId", runId)
            .CreateLogger();
    }
}
