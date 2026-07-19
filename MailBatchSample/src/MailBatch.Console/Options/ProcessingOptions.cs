namespace MailBatch.Console.Options;

/// <summary>
/// メール処理と移動先メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ProcessingOptions
{
    private const int DEFAULT_REQUEST_QUEUE_CAPACITY = 100;

    /// <summary>正常処理したメールの移動先メールボックス名を取得します。</summary>
    public string ProcessedMailbox { get; init; } = "Processed";

    /// <summary>処理に失敗したメールの移動先メールボックス名を取得します。</summary>
    public string ErrorMailbox { get; init; } = "Error";

    /// <summary>API要求キューに保持できる最大件数を取得します。</summary>
    public int RequestQueueCapacity { get; init; } = DEFAULT_REQUEST_QUEUE_CAPACITY;

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
