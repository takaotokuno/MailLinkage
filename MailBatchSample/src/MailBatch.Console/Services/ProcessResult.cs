namespace MailBatch.Console.Services;

internal sealed record ProcessResult(int Total, int Succeeded = 0, int Failed = 0)
{
    /// <summary>
    /// 成功件数を1件加算した新しい処理結果を返します。
    /// </summary>
    public ProcessResult AddSuccess()
    {
        return this with { Succeeded = Succeeded + 1 };
    }

    /// <summary>
    /// 失敗件数を1件加算した新しい処理結果を返します。
    /// </summary>
    public ProcessResult AddFailure()
    {
        return this with { Failed = Failed + 1 };
    }
}
