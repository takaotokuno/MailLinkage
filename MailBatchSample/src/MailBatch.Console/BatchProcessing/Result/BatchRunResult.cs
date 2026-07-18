namespace MailBatch.Console.BatchProcessing.Result;

/// <summary>
/// バッチ実行全体の結果を表します。
/// </summary>
internal sealed record BatchRunResult(ProcessResult ProcessResult, FatalBatchError? FatalError = null)
{
    public bool HasFatalError
    {
        get
        {
            return FatalError is not null;
        }
    }

    public int ConvertToExitCode() => HasFatalError ? 1 : ProcessResult.ConvertToExitCode();
}

/// <summary>
/// バッチ処理を継続できない致命的なエラーを表します。
/// </summary>
internal sealed record FatalBatchError(string Code, string Message, string Stage);
