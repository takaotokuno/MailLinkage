namespace MailBatch.Console.Options;

internal sealed class BatchOptions
{
    public string LogDirectory { get; init; } = "MailBatchSample/logs";

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(LogDirectory, "Batck:LogDirectory");
    }
}
