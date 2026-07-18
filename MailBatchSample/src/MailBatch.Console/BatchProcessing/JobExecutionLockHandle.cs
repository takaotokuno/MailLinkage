namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 取得済みのバッチ実行ロックを表します。
/// </summary>
internal sealed class JobExecutionLockHandle(IDisposable releaseAction) : IDisposable
{
    public void Dispose() => releaseAction.Dispose();
}
