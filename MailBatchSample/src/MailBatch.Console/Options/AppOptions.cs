namespace MailBatch.Console.Options;

internal sealed class AppOptions
{
    public BatchOptions Batch { get; init; } = new();
    public ImapOptions Imap { get; init; } = new();
    public MailSearchOptions MailSearch { get; init; } = new();
    public ApiOptions Api { get; init; } = new();
    public ProcessingOptions Processing { get; init; } = new();
    public MailNotificationOptions Notification { get; init; } = new();

    /// <summary>
    /// 各種アプリケーション設定の必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        Batch.Validate();
        Imap.Validate();
        Api.Validate();
        MailSearch.Validate();
        Processing.Validate();
        Notification.Validate();
    }
}
