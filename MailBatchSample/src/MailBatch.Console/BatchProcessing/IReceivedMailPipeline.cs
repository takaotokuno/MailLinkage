using MailBatch.Console.Models;
using MailKit;

namespace MailBatch.Console.BatchProcessing;

internal interface IReceivedMailPipeline
{
    Task<ProcessResult> ProcessAsync(IReadOnlyList<UniqueId> targetUids);
}
