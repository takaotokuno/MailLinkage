using MailBatch.Console.Configuration;
using Serilog;

namespace MailBatch.Console.Logging;

/// <summary>
/// バッチ実行単位の識別子を付与したSerilogロガーを生成します。
/// </summary>
/// <remarks>
/// log4netではなくSerilogを採用する理由
/// Serilogはjson形式等、構造化されたログを前提としており、障害調査時の検索性がより高いため
/// </remarks>
internal static class SerilogLoggerFactory
{
    private const string OUTPUT_TEMPLATE =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({RunId}) {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// アプリケーション設定を読み込む前のエラーを標準エラーへ出力するロガーを作成します。
    /// </summary>
    public static Serilog.Core.Logger CreateBootstrap(string runId)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.WithProperty("RunId", runId)
            .WriteTo.Console(
                standardErrorFromLevel: Serilog.Events.LogEventLevel.Error,
                outputTemplate: OUTPUT_TEMPLATE)
            .CreateLogger();
    }

    /// <summary>
    /// 読み込み済み設定と実行IDからSerilogロガーを作成します。
    /// </summary>
    public static Serilog.Core.Logger Create(LoadedConfiguration loadedConfiguration, string runId)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(loadedConfiguration.Configuration)
            .Enrich.WithProperty("RunId", runId)
            .CreateLogger();
    }
}
