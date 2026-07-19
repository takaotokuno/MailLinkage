using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.State;

public sealed class SqliteRetentionCleanerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mail-processing-retention-{Guid.NewGuid():N}");

    [Fact]
    public void DeleteExpiredRecords_DeletesOldRecordsAndVacuumsDatabase()
    {
        _ = Directory.CreateDirectory(_directory);
        string databasePath = Path.Combine(_directory, "mail-processing.db");
        using (SqliteConnection connection = new($"Data Source={databasePath}"))
        {
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE processed_mails (uid INTEGER, processed_at_utc TEXT NOT NULL);
                CREATE TABLE mail_move_failures (uid INTEGER, last_failed_at_utc TEXT NOT NULL);
                CREATE TABLE batch_runs (run_id TEXT, ended_at_utc TEXT NOT NULL);
                CREATE TABLE api_execution_results (execution_id TEXT, completed_at_utc TEXT NOT NULL);
                INSERT INTO processed_mails VALUES (1, '2026-06-18T00:00:00.0000000+00:00');
                INSERT INTO processed_mails VALUES (2, '2026-06-19T00:00:00.0000000+00:00');
                INSERT INTO mail_move_failures VALUES (3, '2026-06-01T00:00:00.0000000+00:00');
                INSERT INTO mail_move_failures VALUES (4, '2026-07-18T00:00:00.0000000+00:00');
                INSERT INTO batch_runs VALUES ('old', '2026-06-01T00:00:00.0000000+00:00');
                INSERT INTO batch_runs VALUES ('new', '2026-07-18T00:00:00.0000000+00:00');
                INSERT INTO api_execution_results VALUES ('old', '2026-06-01T00:00:00.0000000+00:00');
                INSERT INTO api_execution_results VALUES ('current', '2026-07-18T00:00:00.0000000+00:00');
                """;
            _ = command.ExecuteNonQuery();
        }

        SqliteRetentionCleaner cleaner = new(
            new BatchOptions { LogDirectory = _directory, LogRetentionDays = 30 },
            new FakeTimeProvider(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero)));

        cleaner.DeleteExpiredRecords();

        using SqliteConnection verificationConnection = new($"Data Source={databasePath}");
        verificationConnection.Open();
        Assert.Equal(2L, ExecuteScalar(verificationConnection, "SELECT uid FROM processed_mails;"));
        Assert.Equal(4L, ExecuteScalar(verificationConnection, "SELECT uid FROM mail_move_failures;"));
        Assert.Equal("new", ExecuteStringScalar(verificationConnection, "SELECT run_id FROM batch_runs;"));
        Assert.Equal("current", ExecuteStringScalar(verificationConnection, "SELECT execution_id FROM api_execution_results;"));
        Assert.Equal(0L, ExecuteScalar(verificationConnection, "PRAGMA freelist_count;"));
    }

    [Fact]
    public void DeleteExpiredRecords_WithMissingDatabase_DoesNotCreateDatabase()
    {
        SqliteRetentionCleaner cleaner = new(
            new BatchOptions { LogDirectory = _directory, LogRetentionDays = 30 });

        cleaner.DeleteExpiredRecords();

        Assert.False(Directory.Exists(_directory));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static string ExecuteStringScalar(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return Convert.ToString(command.ExecuteScalar())!;
    }

    private static long ExecuteScalar(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
