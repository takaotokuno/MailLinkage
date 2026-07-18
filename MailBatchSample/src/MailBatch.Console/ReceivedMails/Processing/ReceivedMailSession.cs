using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Fetching;
using MailBatch.Console.ReceivedMails.Folders;
using MailBatch.Console.ReceivedMails.Imap;
using MailBatch.Console.ReceivedMails.MailKit;
using MailBatch.Console.ReceivedMails.Searching;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.Processing;

/// <summary>
/// 受信メールフォルダ操作の排他制御とユースケース向けの調整を担当します。
/// </summary>
internal sealed class ReceivedMailSession(
    ImapOptions imapOptions,
    ProcessingOptions processingOptions,
    IImapConnection imapConnection,
    IMailFolderProvider mailFolderProvider,
    IMailKitSearcher mailKitSearcher,
    IReceivedMailReader receivedMailReader,
    IProcessedMailMover processedMailMover,
    ILogger<ReceivedMailSession> logger) : IReceivedMailSession
{
    private readonly SemaphoreSlim imapLock = new(1, 1);

    /// <summary>
    /// IMAPサーバーへ接続し、受信メールフォルダと処理済みフォルダを利用可能な状態にします。
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            if (imapConnection.IsConnected && mailFolderProvider.ReceiveFolder?.IsOpen == true)
            {
                return;
            }

            mailFolderProvider.Clear();
            await imapConnection.ConnectAsync(cancellationToken);
            await mailFolderProvider.PrepareFoldersAsync(cancellationToken);

            logger.LogInformation(
                "Connected and prepared IMAP folders. Host={Host}, UserName={UserName}, Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}",
                imapOptions.Host,
                imapOptions.UserName,
                imapOptions.Mailbox,
                processingOptions.ProcessedMailbox);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    /// <summary>
    /// IMAPサーバーから正常に切断します。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            mailFolderProvider.Clear();
            await imapConnection.DisconnectAsync(cancellationToken);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールの受信メールID一覧を取得します。
    /// </summary>
    public async Task<IReadOnlyList<ReceivedMailId>> SearchTargetMessagesAsync(MailSearchCondition condition, int maxMessages, CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            return await mailKitSearcher.SearchTargetMessagesAsync(condition, maxMessages, cancellationToken);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    /// <summary>
    /// 指定された受信メールIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    public async Task<ReceivedMail> CreateRequestAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            return await receivedMailReader.ReadAsync(mailId, cancellationToken);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    public async Task MoveToProcessedMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            await processedMailMover.MoveToProcessedMailboxAsync(mailId, cancellationToken);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    /// <summary>
    /// API連携に失敗したメールを設定されたエラーメールボックスへ移動します。
    /// </summary>
    public async Task MoveToErrorMailboxAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await imapLock.WaitAsync(cancellationToken);
        try
        {
            await processedMailMover.MoveToErrorMailboxAsync(mailId, cancellationToken);
        }
        finally
        {
            _ = imapLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        imapLock.Dispose();
    }
}
