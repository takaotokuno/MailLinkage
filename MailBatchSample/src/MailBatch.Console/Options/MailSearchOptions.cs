namespace MailBatch.Console.Options;

internal sealed class MailSearchOptions
{
    public string? SubjectContains
    {
        get; init;
    }
    public string? From
    {
        get; init;
    }
    public int? SinceDays { get; init; } = 7;
    public int MaxMessages { get; init; } = 100;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate() => OptionValidation.RequirePositive(MaxMessages, "MailSearch:MaxMessages");
}
