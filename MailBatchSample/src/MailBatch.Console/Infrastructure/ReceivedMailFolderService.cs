using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;
using MailBatch.Console.BatchProcessing;
using MailKit;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.Infrastructure;

/// <summary>
/// 受信メールフォルダに対するIMAP操作をアプリケーション層向けに取りまとめます。
/// </summary>
internal sealed class ReceivedMailFolderService(
    IImapConnection connection,
    IMailFolderProvider folderProvider,
    IReceivedMailSearcher searcher,
    IReceivedMailReader reader,
    IProcessedMailMover mover,
    AppOptions options,
    ILogger<ReceivedMailFolderService> logger) : IReceivedMailFolderService
{
    /// <summary>
    /// IMAPサーバーへ接続し、受信メールフォルダと処理済みフォルダを利用可能な状態にします。
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await connection.SyncRoot.WaitAsync(cancellationToken);
        try
        {
            await connection.ConnectAsync(cancellationToken);
            IMailFolder receiveFolder = await folderProvider.GetOpenedReceiveFolderAsync(cancellationToken);
            await folderProvider.GetProcessedFolderAsync(receiveFolder, cancellationToken);

            logger.LogInformation(
                "Connected and prepared IMAP folders. Host={Host}, UserName={UserName}, Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}",
                options.Imap.Host,
                options.Imap.UserName,
                options.Imap.Mailbox,
                options.Processing.ProcessedMailbox);
        }
        finally
        {
            connection.SyncRoot.Release();
        }
    }

    /// <summary>
    /// IMAPサーバーから正常に切断します。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await connection.SyncRoot.WaitAsync(cancellationToken);
        try
        {
            folderProvider.Reset();
            await connection.DisconnectAsync(cancellationToken);
        }
        finally
        {
            connection.SyncRoot.Release();
        }
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    public async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(SearchQuery query, int maxMessages, CancellationToken cancellationToken = default)
    {
        await connection.SyncRoot.WaitAsync(cancellationToken);
        try
        {
            IMailFolder receiveFolder = await folderProvider.GetOpenedReceiveFolderAsync(cancellationToken);
            IReadOnlyList<UniqueId> targetUids = await searcher.SearchAsync(receiveFolder, query, maxMessages, cancellationToken);

            logger.LogInformation(
                "Found {MessageCount} target messages. Mailbox={Mailbox}",
                targetUids.Count,
                options.Imap.Mailbox);

            return targetUids;
        }
        finally
        {
            connection.SyncRoot.Release();
        }
    }

    /// <summary>
    /// 指定されたUIDのメール本文と内部受信日時を取得します。
    /// </summary>
    public async Task<ReceivedMailContent> ReadMessageAsync(UniqueId uid, CancellationToken cancellationToken = default)
    {
        await connection.SyncRoot.WaitAsync(cancellationToken);
        try
        {
            IMailFolder receiveFolder = await folderProvider.GetOpenedReceiveFolderAsync(cancellationToken);
            return await reader.ReadAsync(receiveFolder, uid, cancellationToken);
        }
        finally
        {
            connection.SyncRoot.Release();
        }
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    public async Task MoveToProcessedMailboxAsync(UniqueId uid, string messageId, CancellationToken cancellationToken = default)
    {
        await connection.SyncRoot.WaitAsync(cancellationToken);
        try
        {
            IMailFolder receiveFolder = await folderProvider.GetOpenedReceiveFolderAsync(cancellationToken);
            IMailFolder processedFolder = await folderProvider.GetProcessedFolderAsync(receiveFolder, cancellationToken);
            await mover.MoveAsync(receiveFolder, processedFolder, uid, cancellationToken);
        }
        finally
        {
            connection.SyncRoot.Release();
        }

        logger.LogInformation(
            "Moved processed message. MessageId={MessageId}, DestinationMailbox={DestinationMailbox}",
            messageId,
            options.Processing.ProcessedMailbox);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        await connection.DisposeAsync();
    }
}
