namespace MailBatch.Console.Options;

/// <summary>
/// 処理対象メールの検索条件設定を保持します。
/// </summary>
internal sealed class MailSearchOptions
{
    private const int DEFAULT_SINCE_DAYS = 7;
    private const int DEFAULT_MAX_MESSAGES = 100;

    /// <summary>件名に含まれる文字列を取得します。</summary>
    public string? SubjectContains
    {
        get; init;
    }
    /// <summary>送信元メールアドレスの検索条件を取得します。</summary>
    public string? From
    {
        get; init;
    }
    /// <summary>現在から遡って検索対象とする日数を取得します。</summary>
    public int? SinceDays { get; init; } = DEFAULT_SINCE_DAYS;
    /// <summary>一回のバッチで処理するメールの最大件数を取得します。</summary>
    public int MaxMessages { get; init; } = DEFAULT_MAX_MESSAGES;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate() => OptionValidation.RequirePositive(MaxMessages, "MailSearch:MaxMessages");
}
