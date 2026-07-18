namespace MailBatch.Console.Options;

/// <summary>
/// メール処理と移動先メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ProcessingOptions
{
    public string ProcessedMailbox { get; init; } = "Processed";

    public string ErrorMailbox { get; init; } = "Error";

    public int RequestQueueCapacity { get; init; } = 100;

    /// <summary>
    /// 必須項目を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(ProcessedMailbox, "Processing:ProcessedMailbox");
        OptionValidation.Require(ErrorMailbox, "Processing:ErrorMailbox");
        OptionValidation.RequirePositive(RequestQueueCapacity, "Processing:RequestQueueCapacity");
    }
}
