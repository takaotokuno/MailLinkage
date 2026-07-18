using MailBatch.Console.Configuration;
using Serilog;

namespace MailBatch.Console.Logging;

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
