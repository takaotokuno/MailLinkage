using MailBatch.Console.Logging;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.Logging;

public sealed class LogRetentionCleanerTests : IDisposable
{
    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), $"MailBatchLogs-{Guid.NewGuid():N}");

    // 目的: 保持期間を超えたログだけが削除されることを確認する。
    // 前提・入力: 期限切れログ、保持期間内ログ、ログ以外のファイルを作成する。
    // 期待結果: 期限切れログのみ削除され、保持期間内ログと非ログファイルは残る。
    // 検知したい異常: 有効なログや別種ファイルの誤削除、期限切れログの削除漏れ。
    [Fact]
    public void TryDeleteExpiredLogs_DeletesOnlyLogFilesOlderThanRetentionDays()
    {
        _ = Directory.CreateDirectory(_logDirectory);
        DateTimeOffset now = new(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(now);
        string expiredLog = CreateFile("expired.log", now.AddDays(-31));
        string retainedLog = CreateFile("retained.log", now.AddDays(-30));
        string expiredText = CreateFile("expired.txt", now.AddDays(-31));
        BatchOptions options = new()
        {
            LogDirectory = _logDirectory,
            LogRetentionDays = 30
        };
        LogRetentionCleaner cleaner = new(options, timeProvider);

        Assert.True(cleaner.TryDeleteExpiredLogs());

        Assert.False(File.Exists(expiredLog));
        Assert.True(File.Exists(retainedLog));
        Assert.True(File.Exists(expiredText));
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }

    private string CreateFile(string fileName, DateTimeOffset lastWriteTime)
    {
        string path = Path.Combine(_logDirectory, fileName);
        File.WriteAllText(path, fileName);
        File.SetLastWriteTimeUtc(path, lastWriteTime.UtcDateTime);
        return path;
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone
        {
            get
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
