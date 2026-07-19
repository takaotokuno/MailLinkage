using MailBatch.Console.Options;
using Serilog;

namespace MailBatch.Console.Logging;

/// <summary>
/// 保持期間を過ぎたログファイルを削除します。
/// </summary>
internal sealed class LogRetentionCleaner(BatchOptions batchOptions, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// 設定された保管期間を過ぎたログファイルの削除を試みます。
    /// </summary>
    /// <returns>削除処理が正常に完了した場合は <see langword="true"/>、失敗した場合は <see langword="false"/>。</returns>
    public bool TryDeleteExpiredLogs()
    {
        try
        {
            DeleteExpiredLogs();
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to delete expired log files.");
            return false;
        }
    }

    private void DeleteExpiredLogs()
    {
        if (!Directory.Exists(batchOptions.LogDirectory))
        {
            return;
        }

        DateTimeOffset expirationThreshold = _timeProvider.GetUtcNow().AddDays(-batchOptions.LogRetentionDays);

        IEnumerable<string> logFilePaths = Directory.EnumerateFiles(
            batchOptions.LogDirectory,
            "*.log",
            SearchOption.TopDirectoryOnly);

        foreach (string logFilePath in logFilePaths)
        {
            DateTimeOffset lastWriteTime = File.GetLastWriteTimeUtc(logFilePath);

            if (lastWriteTime < expirationThreshold)
            {
                File.Delete(logFilePath);
            }
        }
    }
}
