using System.Globalization;
using MailBatch.Console.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.State;

/// <summary>
/// 処理済みメール台帳とメール移動失敗を永続化します。
/// </summary>
internal interface IProcessedMailMoveFailureStore
{
    Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default);

    Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// API連携が完了したメールを処理済み台帳へ記録します。
    /// </summary>
    Task RecordProcessedAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// 指定したメールが処理済み台帳に存在するか判定します。
    /// </summary>
    Task<bool> IsProcessedAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

internal readonly record struct MailMoveFailure(ReceivedMailId MailId, MailMoveFailureDestination Destination);

internal enum MailMoveFailureDestination
{
    Processed,
    Error
}

/// <summary>
/// 処理済みメール台帳とメール移動失敗を同一SQLiteデータベースの別テーブルで管理します。
/// </summary>
internal sealed class SqliteMailProcessingStore(
    BatchOptions batchOptions,
    ILogger<SqliteMailProcessingStore> logger) : IProcessedMailMoveFailureStore
{
    private const string DatabaseFileName = "mail-processing.db";
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    private string DatabasePath => Path.Combine(batchOptions.LogDirectory, DatabaseFileName);

    public async Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT uid, uid_validity, destination FROM mail_move_failures ORDER BY uid_validity, uid, destination;";

        List<MailMoveFailure> failures = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            failures.Add(new MailMoveFailure(
                new ReceivedMailId(ToUInt32(reader.GetInt64(0)), ToUInt32(reader.GetInt64(1))),
                Enum.Parse<MailMoveFailureDestination>(reader.GetString(2), ignoreCase: true)));
        }

        return failures;
    }

    public async Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection,
            "SELECT EXISTS(SELECT 1 FROM mail_move_failures WHERE uid = $uid AND uid_validity = $uidValidity);",
            mailId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        AddFailureAsync(mailId, MailMoveFailureDestination.Processed, cancellationToken);

    public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        AddFailureAsync(mailId, MailMoveFailureDestination.Error, cancellationToken);

    public async Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection,
            "DELETE FROM mail_move_failures WHERE uid = $uid AND uid_validity = $uidValidity AND destination = $destination;",
            failure.MailId);
        _ = command.Parameters.AddWithValue("$destination", failure.Destination.ToString());
        if (await command.ExecuteNonQueryAsync(cancellationToken) > 0)
        {
            logger.LogInformation("Cleared mailbox move failure record. MailId={MailId}, Destination={Destination}", failure.MailId, failure.Destination);
        }
    }

    public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        RemoveAsync(new MailMoveFailure(mailId, MailMoveFailureDestination.Processed), cancellationToken);

    public async Task RecordProcessedAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection, """
            INSERT INTO processed_mails (uid, uid_validity, processed_at_utc)
            VALUES ($uid, $uidValidity, $processedAtUtc)
            ON CONFLICT(uid, uid_validity) DO NOTHING;
            """, mailId);
        _ = command.Parameters.AddWithValue("$processedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> IsProcessedAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection,
            "SELECT EXISTS(SELECT 1 FROM processed_mails WHERE uid = $uid AND uid_validity = $uidValidity);",
            mailId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private async Task AddFailureAsync(ReceivedMailId mailId, MailMoveFailureDestination destination, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection, """
            INSERT INTO mail_move_failures (uid, uid_validity, destination, failed_at_utc)
            VALUES ($uid, $uidValidity, $destination, $failedAtUtc)
            ON CONFLICT(uid, uid_validity, destination) DO UPDATE SET failed_at_utc = excluded.failed_at_utc;
            """, mailId);
        _ = command.Parameters.AddWithValue("$destination", destination.ToString());
        _ = command.Parameters.AddWithValue("$failedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogWarning("Recorded mailbox move failure. MailId={MailId}, Destination={Destination}", mailId, destination);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        SqliteConnection connection = new($"Data Source={DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? ".");
            await using SqliteConnection connection = new($"Data Source={DatabasePath}");
            await connection.OpenAsync(cancellationToken);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS processed_mails (
                    uid INTEGER NOT NULL,
                    uid_validity INTEGER NOT NULL,
                    processed_at_utc TEXT NOT NULL,
                    PRIMARY KEY (uid, uid_validity)
                );
                CREATE TABLE IF NOT EXISTS mail_move_failures (
                    uid INTEGER NOT NULL,
                    uid_validity INTEGER NOT NULL,
                    destination TEXT NOT NULL CHECK (destination IN ('Processed', 'Error')),
                    failed_at_utc TEXT NOT NULL,
                    PRIMARY KEY (uid, uid_validity, destination)
                );
                """;
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _ = _initializationLock.Release();
        }
    }

    private static SqliteCommand CreateMailIdCommand(SqliteConnection connection, string commandText, ReceivedMailId mailId)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        _ = command.Parameters.AddWithValue("$uid", (long)mailId.Uid);
        _ = command.Parameters.AddWithValue("$uidValidity", (long)mailId.UidValidity);
        return command;
    }

    private static uint ToUInt32(long value) => checked((uint)value);
}
