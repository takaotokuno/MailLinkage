using System.Globalization;
using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails;
using Microsoft.Data.Sqlite;

namespace MailBatch.Console.Api;

/// <summary>
/// API実行後に確定した結果を、検索・判定用の事実として保存します。
/// </summary>
internal interface IApiExecutionResultStore
{
    /// <summary>確定したAPI実行結果を永続化します。</summary>
    Task RecordAsync(ApiExecutionResult result, CancellationToken cancellationToken = default);
}

/// <summary>一回のAPI要求について保存する実行結果を表します。</summary>
internal sealed record ApiExecutionResult(
    string ExecutionId,
    ReceivedMailId MailId,
    string Endpoint,
    string Outcome,
    int? StatusCode,
    string? SavedId,
    string? ResponseSummary,
    string? ErrorType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMilliseconds);

/// <summary>
/// API実行結果をメール処理DBの専用テーブルへ追記します。
/// リクエスト本文やレスポンス全文は機微情報を含み得るため保存しません。
/// </summary>
internal sealed class SqliteApiExecutionResultStore(
    BatchOptions batchOptions,
    BatchRunContext batchRunContext) : IApiExecutionResultStore
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
    public async Task RecordAsync(ApiExecutionResult result, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenConnectionAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO api_execution_results (
                execution_id, run_id, uid, uid_validity, endpoint, outcome, status_code,
                saved_id, response_summary, error_type, started_at_utc, completed_at_utc, duration_ms)
            VALUES (
                $executionId, $runId, $uid, $uidValidity, $endpoint, $outcome, $statusCode,
                $savedId, $responseSummary, $errorType, $startedAtUtc, $completedAtUtc, $durationMs);
            """;
        _ = command.Parameters.AddWithValue("$executionId", result.ExecutionId);
        _ = command.Parameters.AddWithValue("$runId", batchRunContext.RunId);
        _ = command.Parameters.AddWithValue("$uid", (long)result.MailId.Uid);
        _ = command.Parameters.AddWithValue("$uidValidity", (long)result.MailId.UidValidity);
        _ = command.Parameters.AddWithValue("$endpoint", result.Endpoint);
        _ = command.Parameters.AddWithValue("$outcome", result.Outcome);
        _ = command.Parameters.AddWithValue("$statusCode", (object?)result.StatusCode ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$savedId", (object?)result.SavedId ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$responseSummary", (object?)result.ResponseSummary ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$errorType", (object?)result.ErrorType ?? DBNull.Value);
        _ = command.Parameters.AddWithValue("$startedAtUtc", result.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$completedAtUtc", result.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        _ = command.Parameters.AddWithValue("$durationMs", result.DurationMilliseconds);
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
                CREATE TABLE IF NOT EXISTS api_execution_results (
                    execution_id TEXT NOT NULL PRIMARY KEY,
                    run_id TEXT NOT NULL,
                    uid INTEGER NOT NULL,
                    uid_validity INTEGER NOT NULL,
                    endpoint TEXT NOT NULL,
                    outcome TEXT NOT NULL CHECK (outcome IN ('Succeeded', 'Failed', 'Exception')),
                    status_code INTEGER,
                    saved_id TEXT,
                    response_summary TEXT,
                    error_type TEXT,
                    started_at_utc TEXT NOT NULL,
                    completed_at_utc TEXT NOT NULL,
                    duration_ms INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_api_execution_results_mail
                    ON api_execution_results (uid_validity, uid, completed_at_utc DESC);
                CREATE INDEX IF NOT EXISTS ix_api_execution_results_outcome
                    ON api_execution_results (outcome, completed_at_utc DESC);
                """;
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _ = _initializationLock.Release();
        }
    }
}
