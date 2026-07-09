using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal interface IReceivedMailPipeline
{
    Task<ProcessResult> ProcessAsync(IReadOnlyList<ReceivedMailId> targetMailIds, CancellationToken cancellationToken = default);
}
