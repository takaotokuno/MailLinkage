namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 確定した処理結果を表します。
/// </summary>
internal sealed record ProcessResult(int Total, int Succeeded = 0, int Failed = 0)
{
    public int ConvertToExitCode() => Failed > 0 ? 2 : 0;
}

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

    public int Failed
    {
        get; private set;
    }

    public void IncrementSuccess() => Succeeded++;

    public void IncrementFailure() => Failed++;

    public ProcessResult ToResult() => new(Total, Succeeded, Failed);
}
