namespace MailBatch.Console.BatchProcessing.Result;

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

    /// <summary>
    /// 処理結果をプロセス終了コードへ変換します。
    /// </summary>
    public int ConvertToExitCode() => Failed > 0 ? BatchExitCodes.PROCESSING_FAILURE : BatchExitCodes.SUCCESS;
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

    public int InvalidFormat
    {
        get; private set;
    }

    public int ApiFailed
    {
        get; private set;
    }

    /// <summary>
    /// 成功件数を1件加算します。
    /// </summary>
    public void IncrementSuccess() => Succeeded++;

    /// <summary>
    /// 入力形式不正件数を1件加算します。
    /// </summary>
    public void IncrementInvalidFormat() => InvalidFormat++;

    /// <summary>
    /// API連携失敗件数を1件加算します。
    /// </summary>
    public void IncrementApiFailure() => ApiFailed++;

    /// <summary>
    /// 集計中の値から処理結果を作成します。
    /// </summary>
    public ProcessResult ToResult() => new(Total, Succeeded, InvalidFormat, ApiFailed);
}
