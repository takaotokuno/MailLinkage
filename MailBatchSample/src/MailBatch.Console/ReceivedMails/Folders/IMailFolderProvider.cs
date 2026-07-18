using MailKit;

namespace MailBatch.Console.ReceivedMails.Folders;

/// <summary>
/// 受信・処理済み・エラー用のIMAPフォルダーを準備して提供します。
/// </summary>
internal interface IMailFolderProvider
{
    IMailFolder? ReceiveFolder
    {
        get;
    }

    IMailFolder? ProcessedFolder
    {
        get;
    }

    IMailFolder? ErrorFolder
    {
        get;
    }

    /// <summary>
    /// 処理で利用するメールボックスを準備します。
    /// </summary>
    Task PrepareFoldersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// オープン済みの受信メールボックスを取得します。
    /// </summary>
    IMailFolder GetOpenedReceiveFolder();

    /// <summary>
    /// 処理済みメールボックスを取得または作成します。
    /// </summary>
    Task<IMailFolder> GetOrCreateProcessedFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// エラーメールボックスを取得または作成します。
    /// </summary>
    Task<IMailFolder> GetOrCreateErrorFolderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保持しているメールボックス参照をクリアします。
    /// </summary>
    void Clear();
}
