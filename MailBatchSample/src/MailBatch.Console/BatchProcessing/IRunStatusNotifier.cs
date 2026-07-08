using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// バッチ実行結果を通知します。
/// </summary>
internal interface IRunStatusNotifier
{
    Task NotifyAsync(ProcessResult result, int exitCode);
}
