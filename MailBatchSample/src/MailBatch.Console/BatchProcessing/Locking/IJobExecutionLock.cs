namespace MailBatch.Console.BatchProcessing.Locking;

/// <summary>
/// バッチジョブの多重起動を防止するための実行ロックを提供します。
/// </summary>
internal interface IJobExecutionLock
{
    /// <summary>
    /// 実行ロックを取得します。すでに別プロセスが実行中の場合は null を返します。
    /// </summary>
    JobExecutionLockHandle? TryAcquire();
}
