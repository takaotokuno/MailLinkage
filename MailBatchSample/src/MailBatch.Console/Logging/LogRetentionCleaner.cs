using MailBatch.Console.Options;

namespace MailBatch.Console.Logging;

internal sealed class LogRetentionCleaner(BatchOptions batchOptions, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// 設定された保管期間を過ぎたログファイルを削除します。
    /// </summary>
    public void DeleteExpiredLogs()
    {
        if (!Directory.Exists(batchOptions.LogDirectory))
        {
            return;
        }

        DateTimeOffset expirationThreshold = timeProvider.GetUtcNow().AddDays(-batchOptions.LogRetentionDays);

        foreach (string logFilePath in Directory.EnumerateFiles(batchOptions.LogDirectory, "*.log", SearchOption.TopDirectoryOnly))
        {
            DateTimeOffset lastWriteTime = File.GetLastWriteTimeUtc(logFilePath);

            if (lastWriteTime < expirationThreshold)
            {
                File.Delete(logFilePath);
            }
        }
    }
}
