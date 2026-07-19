using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.State;

public sealed class SqliteMailProcessingStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mail-processing-store-{Guid.NewGuid():N}");

    [Fact]
    public async Task Store_PersistsProcessedLedgerAndMoveFailuresAcrossInstances()
    {
        ReceivedMailId processedMailId = new(10, 1000);
        ReceivedMailId failedMailId = new(20, 1000);
        SqliteMailProcessingStore firstStore = CreateStore();

        await firstStore.RecordProcessedAsync(processedMailId);
        await firstStore.AddErrorMoveFailureAsync(failedMailId);

        SqliteMailProcessingStore reopenedStore = CreateStore();
        Assert.True(await reopenedStore.IsProcessedAsync(processedMailId));
        MailMoveFailure failure = Assert.Single(await reopenedStore.GetAllAsync());
        Assert.Equal(failedMailId, failure.MailId);
        Assert.Equal(MailMoveFailureDestination.Error, failure.Destination);
        Assert.True(failure.CreatedAtUtc <= failure.LastFailedAtUtc);

        await reopenedStore.RemoveAsync(failure);
        Assert.Empty(await reopenedStore.GetAllAsync());
    }

    [Fact]
    public async Task AddFailure_WhenRecordAlreadyExists_PreservesCreatedAtAndUpdatesLastFailedAt()
    {
        ReceivedMailId mailId = new(30, 1000);
        SqliteMailProcessingStore store = CreateStore();

        await store.AddAsync(mailId);
        MailMoveFailure firstFailure = Assert.Single(await store.GetAllAsync());
        await Task.Delay(20);
        await store.AddAsync(mailId);
        MailMoveFailure updatedFailure = Assert.Single(await store.GetAllAsync());

        Assert.Equal(firstFailure.CreatedAtUtc, updatedFailure.CreatedAtUtc);
        Assert.True(updatedFailure.LastFailedAtUtc > firstFailure.LastFailedAtUtc);
    }

    [Fact]
    public async Task RecordRecoveryFailure_UpdatesOnlyLastFailedAt()
    {
        ReceivedMailId mailId = new(40, 1000);
        SqliteMailProcessingStore store = CreateStore();
        await store.AddAsync(mailId);
        MailMoveFailure firstFailure = Assert.Single(await store.GetAllAsync());
        await Task.Delay(20);

        await store.RecordRecoveryFailureAsync(firstFailure);
        MailMoveFailure updatedFailure = Assert.Single(await store.GetAllAsync());

        Assert.Equal(firstFailure.CreatedAtUtc, updatedFailure.CreatedAtUtc);
        Assert.True(updatedFailure.LastFailedAtUtc > firstFailure.LastFailedAtUtc);
    }

    [Fact]
    public async Task Store_WhenLegacyFailureExists_BackfillsBothTimestamps()
    {
        _ = Directory.CreateDirectory(_directory);
        string databasePath = Path.Combine(_directory, "mail-processing.db");
        const string LEGACY_TIMESTAMP = "2026-07-01T01:02:03.0000000+00:00";
        await using (SqliteConnection connection = new($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE mail_move_failures (
                    uid INTEGER NOT NULL,
                    uid_validity INTEGER NOT NULL,
                    destination TEXT NOT NULL,
                    failed_at_utc TEXT NOT NULL,
                    PRIMARY KEY (uid, uid_validity, destination)
                );
                INSERT INTO mail_move_failures (uid, uid_validity, destination, failed_at_utc)
                VALUES (50, 1000, 'Processed', '{LEGACY_TIMESTAMP}');
                """;
            _ = await command.ExecuteNonQueryAsync();
        }

        MailMoveFailure failure = Assert.Single(await CreateStore().GetAllAsync());

        DateTimeOffset expected = DateTimeOffset.Parse(LEGACY_TIMESTAMP);
        Assert.Equal(expected, failure.CreatedAtUtc);
        Assert.Equal(expected, failure.LastFailedAtUtc);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteMailProcessingStore CreateStore() => new(
        new BatchOptions { LogDirectory = _directory },
        NullLogger<SqliteMailProcessingStore>.Instance);
}
