namespace MailBatch.Console.Options;

/// <summary>
/// アプリケーション全体の設定を保持します。
/// </summary>
internal sealed class AppOptions
{
    /// <summary>バッチ実行に関する設定を取得します。</summary>
    public BatchOptions Batch { get; init; } = new();
    /// <summary>IMAP接続に関する設定を取得します。</summary>
    public ImapOptions Imap { get; init; } = new();
    /// <summary>受信メールの検索条件を取得します。</summary>
    public MailSearchOptions MailSearch { get; init; } = new();
    /// <summary>連携先APIに関する設定を取得します。</summary>
    public ApiOptions Api { get; init; } = new();
    /// <summary>メール処理に関する設定を取得します。</summary>
    public ProcessingOptions Processing { get; init; } = new();
    /// <summary>通知メールに関する設定を取得します。</summary>
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
