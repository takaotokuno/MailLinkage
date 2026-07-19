using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing.Locking;

/// <summary>
/// ロックファイルの排他オープンにより、同一環境内のバッチ多重起動を防止します。
/// </summary>
internal sealed class FileJobExecutionLock(
    BatchOptions batchOptions,
    BatchRunContext runContext,
    ILogger<FileJobExecutionLock> logger)
     : IJobExecutionLock
{
    private const string LOCK_FILE_NAME = "MailBatch.Console.lock";

    /// <summary>
    /// バッチ実行ロックの取得を試行します。
    /// </summary>
    public JobExecutionLockHandle? TryAcquire()
    {
        _ = Directory.CreateDirectory(batchOptions.LogDirectory);
        string lockFilePath = Path.Combine(batchOptions.LogDirectory, LOCK_FILE_NAME);

        try
        {
            // OSのファイル共有制御を利用し、別プロセスが同時に同じメールを処理する二重連携を防ぎます。
            // 「同プロセスが起動済か検索し、起動済だった場合終了する」という処理では、バッチを2回同時に起動した場合に競合が起こり得ます。
            // ファイルオープンはOS内で排他的に判定されるため、同時に2プロセスが成功しないことが保証されます。
            FileStream lockFileStream = new(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            lockFileStream.SetLength(0);
            using StreamWriter writer = new(lockFileStream, leaveOpen: true);
            writer.WriteLine($"RunId={runContext.RunId}");
            writer.WriteLine($"ProcessId={Environment.ProcessId}");
            writer.WriteLine($"StartedAt={DateTimeOffset.UtcNow:O}");
            writer.Flush();
            lockFileStream.Flush();
            lockFileStream.Position = 0;

            logger.LogInformation("Job execution lock acquired. LockFilePath={LockFilePath}", lockFilePath);
            return new JobExecutionLockHandle(lockFileStream);
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "Another mail batch process is already running. LockFilePath={LockFilePath}",
                lockFilePath);
            return null;
        }
    }
}

