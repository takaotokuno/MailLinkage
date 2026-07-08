using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// バッチ処理結果からプロセス終了コードを決定します。
/// </summary>
internal interface IExitCodePolicy
{
    int ToExitCode(ProcessResult result);
}
