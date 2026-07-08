using MailBatch.Console.Models;

namespace MailBatch.Console.BatchProcessing;

internal sealed class ExitCodePolicy : IExitCodePolicy
{
    public int DetermineExitCode(ProcessResult result)
    {
        return result.Failed > 0 ? 2 : 0;
    }
}
