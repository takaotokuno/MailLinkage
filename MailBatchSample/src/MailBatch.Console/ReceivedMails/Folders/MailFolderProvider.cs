using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Imap;
using MailKit;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Folders;

/// <summary>
/// 設定に基づいてIMAPフォルダーを取得または作成し、処理中のフォルダー参照を保持します。
/// </summary>
internal sealed class MailFolderProvider(
    ImapOptions imapOptions,
    ProcessingOptions processingOptions,
    IImapConnection imapConnection,
    ILogger<MailFolderProvider> logger) : IMailFolderProvider
{
    /// <summary>処理対象の受信メールフォルダーを取得します。</summary>
    public IMailFolder? ReceiveFolder
    {
        get; private set;
    }

    /// <summary>正常処理したメールの移動先フォルダーを取得します。</summary>
    public IMailFolder? ProcessedFolder
    {
        get; private set;
    }

    /// <summary>処理に失敗したメールの移動先フォルダーを取得します。</summary>
    public IMailFolder? ErrorFolder
    {
        get; private set;
    }

    /// <summary>
    /// 受信、処理済み、エラー用メールボックスを準備します。
    /// </summary>
    public async Task PrepareFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        // 必要なメールボックスを起動時に準備し、処理中に移動先不足でメール状態が中途半端になることを防ぎます。
        ReceiveFolder = await GetOrCreateReceiveFolderAsync(cancellationToken);

        _ = await ReceiveFolder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        ProcessedFolder = await GetOrCreateConfiguredSubfolderAsync(
            ReceiveFolder,
            processingOptions.ProcessedMailbox,
            cancellationToken);
        ErrorFolder = await GetOrCreateConfiguredSubfolderAsync(
            ReceiveFolder,
            processingOptions.ErrorMailbox,
            cancellationToken);

        logger.LogInformation(
            "Prepared IMAP folders. Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}, ErrorMailbox={ErrorMailbox}",
            imapOptions.Mailbox,
            processingOptions.ProcessedMailbox,
            processingOptions.ErrorMailbox);
    }

    /// <summary>
    /// オープン済みの受信メールボックスを取得します。
    /// </summary>
    public IMailFolder GetOpenedReceiveFolder()
    {
        return ReceiveFolder?.IsOpen != true
            ? throw new InvalidOperationException(
                "Receive mailbox is not open. Call PrepareFoldersAsync before operating mail folders.")
            : ReceiveFolder;
    }

    /// <summary>
    /// 処理済みメールボックスを取得または作成します。
    /// </summary>
    public async Task<IMailFolder> GetOrCreateProcessedFolderAsync(
        CancellationToken cancellationToken = default)
    {
        if (ProcessedFolder is null)
        {
            IMailFolder receiveFolder = GetOpenedReceiveFolder();

            ProcessedFolder = await GetOrCreateConfiguredSubfolderAsync(
                receiveFolder,
                processingOptions.ProcessedMailbox,
                cancellationToken);
        }

        return ProcessedFolder;
    }

    /// <summary>
    /// エラーメールボックスを取得または作成します。
    /// </summary>
    public async Task<IMailFolder> GetOrCreateErrorFolderAsync(
        CancellationToken cancellationToken = default)
    {
        if (ErrorFolder is null)
        {
            IMailFolder receiveFolder = GetOpenedReceiveFolder();

            ErrorFolder = await GetOrCreateConfiguredSubfolderAsync(
                receiveFolder,
                processingOptions.ErrorMailbox,
                cancellationToken);
        }

        return ErrorFolder;
    }

    /// <summary>
    /// 保持しているメールボックス参照をクリアします。
    /// </summary>
    public void Clear()
    {
        ReceiveFolder = null;
        ProcessedFolder = null;
        ErrorFolder = null;
    }

    /// <summary>
    /// 受信メールボックスを取得または作成して開きます。
    /// </summary>
    private async Task<IMailFolder> GetOrCreateReceiveFolderAsync(
        CancellationToken cancellationToken)
    {
        string folderName = imapOptions.Mailbox;

        try
        {
            return await imapConnection.Client.GetFolderAsync(
                folderName,
                cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            IMailFolder namespaceRoot = GetPersonalNamespaceRoot();

            return await CreateFolderAsync(namespaceRoot, folderName, cancellationToken);
        }
    }

    /// <summary>
    /// 設定されたサブフォルダーを取得または作成します。
    /// </summary>
    private static async Task<IMailFolder> GetOrCreateConfiguredSubfolderAsync(
        IMailFolder receiveFolder,
        string folderName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await receiveFolder.GetSubfolderAsync(
                folderName,
                cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            return await CreateFolderAsync(
                receiveFolder,
                folderName,
                cancellationToken);
        }
    }

    /// <summary>
    /// 個人名前空間のルートメールボックスを取得します。
    /// </summary>
    private IMailFolder GetPersonalNamespaceRoot()
    {
        if (imapConnection.Client.PersonalNamespaces.Count == 0)
        {
            throw new InvalidOperationException(
                "The IMAP server does not expose a personal namespace.");
        }

        FolderNamespace personalNamespace =
            imapConnection.Client.PersonalNamespaces[0];

        return imapConnection.Client.GetFolder(personalNamespace);
    }

    /// <summary>
    /// 指定された親メールボックス配下にフォルダーを作成します。
    /// </summary>
    private static async Task<IMailFolder> CreateFolderAsync(
        IMailFolder parentFolder,
        string folderName,
        CancellationToken cancellationToken)
    {
        IMailFolder? createdFolder = await parentFolder.CreateAsync(
            folderName,
            isMessageFolder: true,
            cancellationToken);

        // MailKitの契約上、作成失敗時は自動で例外がスローされます。
        return createdFolder!;
    }
}
