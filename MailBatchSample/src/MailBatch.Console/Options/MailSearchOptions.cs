namespace MailBatch.Console.Options;

/// <summary>
/// 処理対象メールの検索条件設定を保持します。
/// </summary>
internal sealed class MailSearchOptions
{
    private const int DEFAULT_SINCE_DAYS = 7;
    private const int DEFAULT_MAX_MESSAGES = 100;

    public string? SubjectContains
    {
        get; init;
    }
    public string? From
    {
        get; init;
    }
    public int? SinceDays { get; init; } = DEFAULT_SINCE_DAYS;
    public int MaxMessages { get; init; } = DEFAULT_MAX_MESSAGES;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate() => OptionValidation.RequirePositive(MaxMessages, "MailSearch:MaxMessages");
}
