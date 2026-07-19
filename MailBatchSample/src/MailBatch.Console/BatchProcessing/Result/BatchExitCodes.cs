namespace MailBatch.Console.BatchProcessing.Result;

/// <summary>
/// バッチプロセスが返す終了コードを定義します。
/// </summary>
internal static class BatchExitCodes
{
    public const int SUCCESS = 0;
    public const int FATAL_ERROR = 1;
    public const int PROCESSING_FAILURE = 2;
    public const int CANCELED = 130;
}
