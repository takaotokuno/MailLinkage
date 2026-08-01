using MailBatch.Console.Logging;
using MailBatch.Console.ReceivedMails.State;

namespace MailBatch.Console.BatchProcessing;

/// <summary>バッチ完了後に保持期間を過ぎたデータを削除します。</summary>
internal interface IBatchDataRetentionService
{
    /// <summary>ログとバッチ状態データの削除を試みます。</summary>
    void TryDeleteExpiredData();
}

/// <summary>ログとSQLiteの保持期間切れデータを削除します。</summary>
internal sealed class BatchDataRetentionService(
    LogRetentionCleaner logRetentionCleaner,
    SqliteRetentionCleaner sqliteRetentionCleaner) : IBatchDataRetentionService
{
    /// <inheritdoc />
    public void TryDeleteExpiredData()
    {
        _ = logRetentionCleaner.TryDeleteExpiredLogs();
        _ = sqliteRetentionCleaner.TryDeleteExpiredRecords();
    }
}
