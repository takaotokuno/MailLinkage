using MailBatch.Console.Api;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class SqliteApiExecutionResultStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"api-results-{Guid.NewGuid():N}");

    [Fact]
    public async Task RecordAsync_PersistsSearchableResultWithoutPayload()
    {
        SqliteApiExecutionResultStore store = new(
            new BatchOptions { LogDirectory = _directory },
            new BatchRunContext("run-123"));
        ApiExecutionResult result = new(
            "execution-123",
            new ReceivedMailId(42, 99),
            "/api/received-mails",
            "Succeeded",
            201,
            "1001",
            null,
            null,
            new DateTimeOffset(2026, 7, 19, 1, 2, 3, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 19, 1, 2, 4, TimeSpan.Zero),
            1000);

        await store.RecordAsync(result);

        await using SqliteConnection connection = new($"Data Source={Path.Combine(_directory, "mail-processing.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT run_id, uid, uid_validity, outcome, status_code, saved_id, duration_ms FROM api_execution_results WHERE execution_id = 'execution-123';";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("run-123", reader.GetString(0));
        Assert.Equal(42, reader.GetInt64(1));
        Assert.Equal(99, reader.GetInt64(2));
        Assert.Equal("Succeeded", reader.GetString(3));
        Assert.Equal(201, reader.GetInt32(4));
        Assert.Equal("1001", reader.GetString(5));
        Assert.Equal(1000, reader.GetInt64(6));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
