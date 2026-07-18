using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// ロックファイルの排他オープンにより、同一環境内のバッチ多重起動を防止します。
/// </summary>
internal sealed class FileJobExecutionLock(BatchOptions batchOptions, BatchRunContext runContext, ILogger<FileJobExecutionLock> logger) : IJobExecutionLock
{
    private const string LockFileName = "MailBatch.Console.lock";

    public JobExecutionLockHandle? TryAcquire()
    {
        _ = Directory.CreateDirectory(batchOptions.LogDirectory);
        string lockFilePath = Path.Combine(batchOptions.LogDirectory, LockFileName);

        try
        {
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
