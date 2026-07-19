namespace MailBatch.Console.Options;

/// <summary>
/// バッチ実行時のログや件数制御に関する設定を保持します。
/// </summary>
internal sealed class BatchOptions
{
    private const int DEFAULT_LOG_RETENTION_DAYS = 30;

    /// <summary>ログと処理状態データベースを保存するディレクトリを取得します。</summary>
    public string LogDirectory { get; init; } = "MailBatchSample/logs";

    /// <summary>ログを保持する日数を取得します。</summary>
    public int LogRetentionDays { get; init; } = DEFAULT_LOG_RETENTION_DAYS;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(LogDirectory, "Batch:LogDirectory");
        OptionValidation.RequirePositive(LogRetentionDays, "Batch:LogRetentionDays");
    }
}
