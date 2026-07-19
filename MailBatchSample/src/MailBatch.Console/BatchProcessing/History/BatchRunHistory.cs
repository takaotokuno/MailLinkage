using MailBatch.Console.BatchProcessing.Result;

namespace MailBatch.Console.BatchProcessing.History;

/// <summary>
/// 永続化した一回のバッチ実行結果を表します。
/// </summary>
internal sealed record BatchRunHistory(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int ExitCode,
    int Total,
    int Succeeded,
    int InvalidFormat,
    int ApiFailed,
    string? FatalErrorCode,
    string? FatalErrorStage)
{
    public TimeSpan Duration
    {
        get
        {
            return EndedAt - StartedAt;
        }
    }

    public static BatchRunHistory From(string runId, BatchRunResult result, int exitCode) => new(
        runId,
        result.StartedAt,
        result.EndedAt,
        exitCode,
        result.ProcessResult.Total,
        result.ProcessResult.Succeeded,
        result.ProcessResult.InvalidFormat,
        result.ProcessResult.ApiFailed,
        result.FatalError?.Code,
        result.FatalError?.Stage);
}
