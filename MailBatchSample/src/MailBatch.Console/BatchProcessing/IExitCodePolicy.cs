using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal interface IExitCodePolicy
{
    int DetermineExitCode(ProcessResult result);
}
