namespace MailBatch.Console.Options;

internal sealed class BatchOptions
{
    public string LogDirectory { get; init; } = "MailBatchSample/logs";

    public int LogRetentionDays { get; init; } = 30;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(LogDirectory, "Batch:LogDirectory");
        OptionValidation.RequirePositive(LogRetentionDays, "Batch:LogRetentionDays");
    }
}
