namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 確定した処理結果を表します。
/// </summary>
internal sealed record ProcessResult(int Total, int Succeeded = 0, int InvalidFormat = 0, int ApiFailed = 0)
{
    public int Failed
    {
        get
        {
            return InvalidFormat + ApiFailed;
        }
    }

    public int ConvertToExitCode() => Failed > 0 ? 2 : 0;
}

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

/// <summary>
/// 処理途中の集計値を保持します。
/// </summary>
internal sealed class ProcessResultAccumulator(int total = 0)
{
    public int Total { get; private set; } = total;

    public int Succeeded
    {
        get; private set;
    }

    public int InvalidFormat
    {
        get; private set;
    }

    public int ApiFailed
    {
        get; private set;
    }

    public void IncrementSuccess() => Succeeded++;

    public void IncrementInvalidFormat() => InvalidFormat++;

    public void IncrementApiFailure() => ApiFailed++;

    public ProcessResult ToResult() => new(Total, Succeeded, InvalidFormat, ApiFailed);
}
