namespace MailBatch.Console.BatchProcessing.Locking;

/// <summary>
/// 取得済みのバッチ実行ロックを表します。
/// バッチ処理の終了時や例外発生時に、確実にロックが解放されるようにします。
/// </summary>
internal sealed class JobExecutionLockHandle(IDisposable lockResource) : IDisposable
{
    public void Dispose() => lockResource.Dispose();
}
