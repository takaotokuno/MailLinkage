using MailBatch.Console.Options;

namespace MailBatch.Console.Logging;

/// <summary>
/// 保持期間を過ぎたログファイルを削除します。
/// </summary>
internal sealed class LogRetentionCleaner(BatchOptions batchOptions, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// 設定された保管期間を過ぎたログファイルを削除します。
    /// </summary>
    public void DeleteExpiredLogs()
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
