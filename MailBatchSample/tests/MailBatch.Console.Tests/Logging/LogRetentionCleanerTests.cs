using MailBatch.Console.Logging;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.Logging;

public sealed class LogRetentionCleanerTests : IDisposable
{
    private readonly string logDirectory = Path.Combine(Path.GetTempPath(), $"MailBatchLogs-{Guid.NewGuid():N}");

    [Fact]
    public void DeleteExpiredLogs_DeletesOnlyLogFilesOlderThanRetentionDays()
    {
        Directory.CreateDirectory(logDirectory);
        DateTimeOffset now = new(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider timeProvider = new(now);
        string expiredLog = CreateFile("expired.log", now.AddDays(-31));
        string retainedLog = CreateFile("retained.log", now.AddDays(-30));
        string expiredText = CreateFile("expired.txt", now.AddDays(-31));
        BatchOptions options = new()
        {
            LogDirectory = logDirectory,
            LogRetentionDays = 30
        };
        LogRetentionCleaner cleaner = new(options, timeProvider);

        cleaner.DeleteExpiredLogs();

        Assert.False(File.Exists(expiredLog));
        Assert.True(File.Exists(retainedLog));
        Assert.True(File.Exists(expiredText));
    }

    [Fact]
    public void DeleteExpiredLogs_WithMissingLogDirectory_DoesNotThrow()
    {
        BatchOptions options = new()
        {
            LogDirectory = logDirectory,
            LogRetentionDays = 30
        };
        LogRetentionCleaner cleaner = new(options, new FakeTimeProvider(DateTimeOffset.UtcNow));

        Exception? exception = Record.Exception(cleaner.DeleteExpiredLogs);

        Assert.Null(exception);
    }

    public void Dispose()
    {
        if (Directory.Exists(logDirectory))
        {
            Directory.Delete(logDirectory, recursive: true);
        }
    }

    private string CreateFile(string fileName, DateTimeOffset lastWriteTime)
    {
        string path = Path.Combine(logDirectory, fileName);
        File.WriteAllText(path, fileName);
        File.SetLastWriteTimeUtc(path, lastWriteTime.UtcDateTime);
        return path;
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
