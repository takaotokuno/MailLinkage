using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 失敗件数の有無に基づく標準の終了コード判定を提供します。
/// </summary>
internal sealed class ExitCodePolicy : IExitCodePolicy
{
    public int ToExitCode(ProcessResult result)
    {
        return result.Failed > 0 ? 2 : 0;
    }
}
