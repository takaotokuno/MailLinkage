using System.Globalization;
using MailBatch.Console.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.State;

/// <summary>
/// API連携済みメールを記録し、再送を防止する処理済み台帳を提供します。
/// </summary>
internal interface IProcessedMailStore
{
    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task RecordAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>
/// メールボックス移動失敗の記録と復旧状態を提供します。
/// </summary>
internal interface IMailMoveFailureStore
{
    /// <summary>記録されているすべてのメール移動失敗を取得します。</summary>
    Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>指定したメールの移動失敗が記録されているか判定します。</summary>
    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>処理済みメールボックスへの移動失敗を記録します。</summary>
    Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>エラーメールボックスへの移動失敗を記録します。</summary>
    Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    /// <summary>復旧処理で再度失敗した日時を更新します。</summary>
    Task RecordRecoveryFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken = default);

    /// <summary>指定したメール移動失敗の記録を削除します。</summary>
    Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default);

    /// <summary>指定したメールの処理済みフォルダーへの移動失敗記録を削除します。</summary>
    Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>メールボックスへの移動に失敗したメールと失敗日時を表します。</summary>
internal readonly record struct MailMoveFailure(
    ReceivedMailId MailId,
    MailMoveFailureDestination Destination,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastFailedAtUtc)
{
    public MailMoveFailure(ReceivedMailId mailId, MailMoveFailureDestination destination)
        : this(mailId, destination, default, default)
    {
    }
}

/// <summary>メール移動に失敗した際の移動先を表します。</summary>
internal enum MailMoveFailureDestination
{
    /// <summary>正常処理済みメールの移動先です。</summary>
    Processed,
    /// <summary>処理失敗メールの移動先です。</summary>
    Error
}

/// <summary>
/// 処理済みメール台帳とメール移動失敗を同一SQLiteデータベースの別テーブルで管理します。
/// </summary>
internal sealed class SqliteMailProcessingStore(
    BatchOptions batchOptions,
    ILogger<SqliteMailProcessingStore> logger) : IProcessedMailStore, IMailMoveFailureStore
{
    private const string DATABASE_FILE_NAME = "mail-processing.db";
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    private string DatabasePath
    {
        get
        {
            return Path.Combine(batchOptions.LogDirectory, DATABASE_FILE_NAME);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT uid, uid_validity, destination, created_at_utc, last_failed_at_utc FROM mail_move_failures ORDER BY uid_validity, uid, destination;";

        List<MailMoveFailure> failures = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        int uidOrdinal = reader.GetOrdinal("uid");
        int uidValidityOrdinal = reader.GetOrdinal("uid_validity");
        int destinationOrdinal = reader.GetOrdinal("destination");
        int createdAtOrdinal = reader.GetOrdinal("created_at_utc");
        int lastFailedAtOrdinal = reader.GetOrdinal("last_failed_at_utc");
        while (await reader.ReadAsync(cancellationToken))
        {
            failures.Add(new MailMoveFailure(
                new ReceivedMailId(
                    ToUInt32(reader.GetInt64(uidOrdinal)),
                    ToUInt32(reader.GetInt64(uidValidityOrdinal))),
                Enum.Parse<MailMoveFailureDestination>(reader.GetString(destinationOrdinal), ignoreCase: true),
                ParseTimestamp(reader.GetString(createdAtOrdinal)),
                ParseTimestamp(reader.GetString(lastFailedAtOrdinal))));
        }

        return failures;
    }

    /// <inheritdoc />
    public async Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection,
            "SELECT EXISTS(SELECT 1 FROM mail_move_failures WHERE uid = $uid AND uid_validity = $uidValidity);",
            mailId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    /// <inheritdoc />
    public Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        AddFailureAsync(mailId, MailMoveFailureDestination.Processed, cancellationToken);

    /// <inheritdoc />
    public Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        AddFailureAsync(mailId, MailMoveFailureDestination.Error, cancellationToken);

    /// <inheritdoc />
    public Task RecordRecoveryFailureAsync(MailMoveFailure failure, CancellationToken cancellationToken = default) =>
        UpdateLastFailedAtAsync(failure, cancellationToken);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) =>
        RemoveAsync(new MailMoveFailure(mailId, MailMoveFailureDestination.Processed, default, default), cancellationToken);

    public async Task RecordAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
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

    async Task<bool> IProcessedMailStore.ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken)
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
            INSERT INTO mail_move_failures (uid, uid_validity, destination, failed_at_utc, created_at_utc, last_failed_at_utc)
            VALUES ($uid, $uidValidity, $destination, $failedAtUtc, $failedAtUtc, $failedAtUtc)
            ON CONFLICT(uid, uid_validity, destination) DO UPDATE SET
                failed_at_utc = excluded.failed_at_utc,
                last_failed_at_utc = excluded.last_failed_at_utc;
            """, mailId);
        _ = command.Parameters.AddWithValue("$destination", destination.ToString());
        _ = command.Parameters.AddWithValue("$failedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
        logger.LogWarning("Recorded mailbox move failure. MailId={MailId}, Destination={Destination}", mailId, destination);
    }

    private async Task UpdateLastFailedAtAsync(MailMoveFailure failure, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = CreateMailIdCommand(connection, """
            UPDATE mail_move_failures
            SET failed_at_utc = $failedAtUtc, last_failed_at_utc = $failedAtUtc
            WHERE uid = $uid AND uid_validity = $uidValidity AND destination = $destination;
            """, failure.MailId);
        _ = command.Parameters.AddWithValue("$destination", failure.Destination.ToString());
        _ = command.Parameters.AddWithValue("$failedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
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
                    created_at_utc TEXT NOT NULL,
                    last_failed_at_utc TEXT NOT NULL,
                    PRIMARY KEY (uid, uid_validity, destination)
                );
                """;
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
            await AddTimestampColumnIfMissingAsync(connection, "created_at_utc", cancellationToken);
            await AddTimestampColumnIfMissingAsync(connection, "last_failed_at_utc", cancellationToken);
            await using SqliteCommand backfillCommand = connection.CreateCommand();
            backfillCommand.CommandText = """
                UPDATE mail_move_failures
                SET created_at_utc = COALESCE(created_at_utc, failed_at_utc),
                    last_failed_at_utc = COALESCE(last_failed_at_utc, failed_at_utc);
                """;
            _ = await backfillCommand.ExecuteNonQueryAsync(cancellationToken);
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

    private static async Task AddTimestampColumnIfMissingAsync(SqliteConnection connection, string columnName, CancellationToken cancellationToken)
    {
        await using SqliteCommand columnsCommand = connection.CreateCommand();
        columnsCommand.CommandText = "PRAGMA table_info(mail_move_failures);";
        bool exists = false;
        await using (SqliteDataReader reader = await columnsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (!exists)
        {
            await using SqliteCommand alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE mail_move_failures ADD COLUMN {columnName} TEXT;";
            _ = await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static uint ToUInt32(long value) => checked((uint)value);
}
