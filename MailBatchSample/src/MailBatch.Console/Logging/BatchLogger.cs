using Microsoft.Extensions.Configuration;
using Serilog;

namespace MailBatch.Console.Logging;

internal static class BatchLogger
{
    /// <summary>
    /// appsettings.json などから読み込んだ Serilog 設定と実行IDを使用して、アプリケーションロガーを作成します。
    /// </summary>
    public static ILogger Create(IConfiguration configuration, string runId)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.WithProperty("RunId", runId)
            .CreateLogger();
    }
}
