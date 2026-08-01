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

    // 目的: 処理済み記録と移動失敗記録が再生成後も保持されることを確認する。
    // 前提・入力: 処理済みIDとエラー移動失敗IDを保存し、ストアを作り直す。
    // 期待結果: 新しいストアから処理済みIDと移動失敗の種別・IDを取得できる。
    // 検知したい異常: SQLite再接続後に処理状態または移動失敗が消失する不具合。
    [Fact]
    public async Task Store_PersistsProcessedLedgerAndMoveFailuresAcrossInstances()
    {
        ReceivedMailId processedMailId = new(10, 1000);
        ReceivedMailId failedMailId = new(20, 1000);
        SqliteMailProcessingStore firstStore = CreateStore();

        await firstStore.RecordAsync(processedMailId);
        await firstStore.AddErrorMoveFailureAsync(failedMailId);

        SqliteMailProcessingStore reopenedStore = CreateStore();
        Assert.True(await ((IProcessedMailStore)reopenedStore).ContainsAsync(processedMailId));
        MailMoveFailure failure = Assert.Single(await reopenedStore.GetAllAsync());
        Assert.Equal(failedMailId, failure.MailId);
        Assert.Equal(MailMoveFailureDestination.Error, failure.Destination);
        Assert.True(failure.CreatedAtUtc <= failure.LastFailedAtUtc);

        await reopenedStore.RemoveAsync(failure);
        Assert.Empty(await reopenedStore.GetAllAsync());
    }

    // 目的: 同一失敗の再記録で初回日時を保持できることを確認する。
    // 前提・入力: 同じメールIDの移動失敗を時間を空けて2回追加する。
    // 期待結果: CreatedAtUtcは初回値のまま、LastFailedAtUtcだけが新しい日時になる。
    // 検知したい異常: 再失敗時に初回発生日時まで上書きする不具合。
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

    // 目的: 復旧再失敗時に最終失敗日時だけ更新することを確認する。
    // 前提・入力: 保存済み移動失敗に対して復旧失敗を記録する。
    // 期待結果: CreatedAtUtcは変わらず、LastFailedAtUtcだけが後の日時へ更新される。
    // 検知したい異常: 復旧失敗の記録で初回発生日時を失う不具合。
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

    // 目的: 旧スキーマの失敗日時を新しい日時列へ移行できることを確認する。
    // 前提・入力: 単一の旧timestamp列を持つ移動失敗レコードをSQLiteに作成する。
    // 期待結果: CreatedAtUtcとLastFailedAtUtcの両方に旧timestamp値が設定される。
    // 検知したい異常: スキーマ移行後に既存失敗の日時が欠損する不具合。
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
