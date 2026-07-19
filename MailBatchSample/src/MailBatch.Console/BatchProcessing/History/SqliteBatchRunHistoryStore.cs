using System.Globalization;
using MailBatch.Console.Options;
using Microsoft.Data.Sqlite;

namespace MailBatch.Console.BatchProcessing.History;

internal interface IBatchRunHistoryStore
{
    Task AddAsync(BatchRunHistory history, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BatchRunHistory>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}

/// <summary>
/// バッチ実行履歴をメール処理台帳と同じSQLiteデータベースへ保存します。
/// </summary>
internal sealed class SqliteBatchRunHistoryStore(BatchOptions batchOptions) : IBatchRunHistoryStore
{
    private const string DatabaseFileName = "mail-processing.db";
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    private string DatabasePath => Path.Combine(batchOptions.LogDirectory, DatabaseFileName);

    public async Task AddAsync(BatchRunHistory history, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO batch_runs (
                run_id, started_at_utc, ended_at_utc, exit_code,
                total_count, succeeded_count, invalid_format_count, api_failed_count,
                fatal_error_code, fatal_error_stage)
            VALUES (
                $runId, $startedAtUtc, $endedAtUtc, $exitCode,
                $total, $succeeded, $invalidFormat, $apiFailed,
                $fatalErrorCode, $fatalErrorStage);
            """;
        _ = command.Parameters.AddWithValue("$runId", history.RunId);
        _ = command.Parameters.AddWithValue("$startedAtUtc", FormatTimestamp(history.StartedAt));
        _ = command.Parameters.AddWithValue("$endedAtUtc", FormatTimestamp(history.EndedAt));
        _ = command.Parameters.AddWithValue("$exitCode", history.ExitCode);
        _ = command.Parameters.AddWithValue("$total", history.Total);
        _ = command.Parameters.AddWithValue("$succeeded", history.Succeeded);
        _ = command.Parameters.AddWithValue("$invalidFormat", history.InvalidFormat);
        _ = command.Parameters.AddWithValue("$apiFailed", history.ApiFailed);
        _ = command.Parameters.AddWithValue("$fatalErrorCode", (object?)history.FatalErrorCode ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$fatalErrorStage", (object?)history.FatalErrorStage ?? DBNull.Value);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BatchRunHistory>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT run_id, started_at_utc, ended_at_utc, exit_code,
                   total_count, succeeded_count, invalid_format_count, api_failed_count,
                   fatal_error_code, fatal_error_stage
            FROM batch_runs
            ORDER BY ended_at_utc DESC
            LIMIT $count;
            """;
        _ = command.Parameters.AddWithValue("$count", count);

        List<BatchRunHistory> history = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new BatchRunHistory(
                reader.GetString(0),
                ParseTimestamp(reader.GetString(1)),
                ParseTimestamp(reader.GetString(2)),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return history;
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
                CREATE TABLE IF NOT EXISTS batch_runs (
                    run_id TEXT NOT NULL PRIMARY KEY,
                    started_at_utc TEXT NOT NULL,
                    ended_at_utc TEXT NOT NULL,
                    exit_code INTEGER NOT NULL,
                    total_count INTEGER NOT NULL,
                    succeeded_count INTEGER NOT NULL,
                    invalid_format_count INTEGER NOT NULL,
                    api_failed_count INTEGER NOT NULL,
                    fatal_error_code TEXT NULL,
                    fatal_error_stage TEXT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_batch_runs_ended_at_utc
                    ON batch_runs(ended_at_utc DESC);
                """;
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _ = _initializationLock.Release();
        }
    }

    private static string FormatTimestamp(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
