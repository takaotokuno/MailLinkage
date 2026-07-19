using System.Globalization;
using MailBatch.Console.Options;
using Microsoft.Data.Sqlite;

namespace MailBatch.Console.ReceivedMails.State;

/// <summary>
/// 保持期間を過ぎたメール処理レコードを削除し、データベースの空き領域を回収します。
/// </summary>
internal sealed class SqliteRetentionCleaner(
    BatchOptions batchOptions,
    TimeProvider? timeProvider = null)
{
    private const string DatabaseFileName = "mail-processing.db";
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// 古いレコードを物理削除し、削除があった場合はVACUUMでファイルを縮小します。
    /// </summary>
    public void DeleteExpiredRecords()
    {
        string databasePath = Path.Combine(batchOptions.LogDirectory, DatabaseFileName);
        if (!File.Exists(databasePath))
        {
            return;
        }

        string expirationThreshold = _timeProvider.GetUtcNow()
            .AddDays(-batchOptions.LogRetentionDays)
            .ToString("O", CultureInfo.InvariantCulture);

        using SqliteConnection connection = new($"Data Source={databasePath}");
        connection.Open();

        int deletedRecordCount = 0;
        using (SqliteTransaction transaction = connection.BeginTransaction())
        {
            deletedRecordCount += DeleteExpiredRecordsFromExistingTable(
                connection,
                transaction,
                "processed_mails",
                "processed_at_utc",
                expirationThreshold);
            deletedRecordCount += DeleteExpiredRecordsFromExistingTable(
                connection,
                transaction,
                "mail_move_failures",
                "last_failed_at_utc",
                expirationThreshold);
            transaction.Commit();
        }

        if (deletedRecordCount > 0)
        {
            using SqliteCommand vacuumCommand = connection.CreateCommand();
            vacuumCommand.CommandText = "VACUUM;";
            _ = vacuumCommand.ExecuteNonQuery();
        }
    }

    private static int DeleteExpiredRecordsFromExistingTable(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string timestampColumnName,
        string expirationThreshold)
    {
        using SqliteCommand tableExistsCommand = connection.CreateCommand();
        tableExistsCommand.Transaction = transaction;
        tableExistsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName);";
        _ = tableExistsCommand.Parameters.AddWithValue("$tableName", tableName);
        if (Convert.ToInt64(tableExistsCommand.ExecuteScalar(), CultureInfo.InvariantCulture) != 1)
        {
            return 0;
        }

        // タイムスタンプ列追加前のDBでも、従来列を使って安全にクリーンアップする。
        if (!ColumnExists(connection, transaction, tableName, timestampColumnName))
        {
            const string legacyFailureTimestampColumnName = "failed_at_utc";
            if (tableName != "mail_move_failures"
                || !ColumnExists(connection, transaction, tableName, legacyFailureTimestampColumnName))
            {
                return 0;
            }

            timestampColumnName = legacyFailureTimestampColumnName;
        }

        using SqliteCommand deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = $"DELETE FROM {tableName} WHERE {timestampColumnName} < $expirationThreshold;";
        _ = deleteCommand.Parameters.AddWithValue("$expirationThreshold", expirationThreshold);
        return deleteCommand.ExecuteNonQuery();
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT EXISTS(SELECT 1 FROM pragma_table_info('{tableName}') WHERE name = $columnName);";
        _ = command.Parameters.AddWithValue("$columnName", columnName);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }
}
