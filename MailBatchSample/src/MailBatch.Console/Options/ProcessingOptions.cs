namespace MailBatch.Console.Options;

internal sealed class ProcessingOptions
{
    public string ProcessedMailbox { get; init; } = "Processed";

    public string ErrorMailbox { get; init; } = "Error";

    /// <summary>
    /// 必須項目を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(ProcessedMailbox, "Processing:ProcessedMailbox");
        OptionValidation.Require(ErrorMailbox, "Processing:ErrorMailbox");
    }
}
