namespace MailBatch.Console.BatchProcessing.Result;

/// <summary>
/// バッチ実行全体の結果を表します。
/// </summary>
internal sealed record BatchRunResult(
    ProcessResult ProcessResult,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    FatalBatchError? FatalError = null)
{
    /// <summary>バッチを継続できない致命的なエラーが発生したかどうかを取得します。</summary>
    public bool HasFatalError
    {
        get
        {
            return FatalError is not null;
        }
    }

    /// <summary>
    /// 処理結果をプロセス終了コードへ変換します。
    /// </summary>
    public int ConvertToExitCode() => HasFatalError ? BatchExitCodes.FATAL_ERROR : ProcessResult.ConvertToExitCode();
}

/// <summary>
/// バッチ処理を継続できない致命的なエラーを表します。
/// </summary>
internal sealed record FatalBatchError(string Code, string Message, string Stage);
