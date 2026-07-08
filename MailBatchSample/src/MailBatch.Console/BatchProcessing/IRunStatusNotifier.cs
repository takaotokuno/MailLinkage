using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal interface IRunStatusNotifier
{
    Task NotifyAsync(ProcessResult result, int exitCode, CancellationToken cancellationToken = default);
}
