using MailBatch.Console.Mail;
using MailBatch.Console.Models;
using MailBatch.Console.Options;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MailBatch.Console.Services;

/// <summary>
/// 受信メールフォルダに対するIMAP操作を一元管理します。
/// </summary>
internal sealed class ReceivedMailFolderService(
    AppOptions options,
    ILogger<ReceivedMailFolderService> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim imapLock = new(1, 1);
    private ImapClient? imapClient;
    private IMailFolder? receiveFolder;
    private IMailFolder? processedFolder;

    /// <summary>
    /// IMAPサーバーへ接続し、受信メールフォルダと処理済みフォルダを利用可能な状態にします。
    /// </summary>
    public async Task ConnectAsync()
    {
        await imapLock.WaitAsync();
        try
        {
            if (imapClient?.IsConnected == true && receiveFolder?.IsOpen == true)
            {
                return;
            }

            imapClient?.Dispose();
            imapClient = new ImapClient();

            logger.LogInformation(
                "Connecting to IMAP server. Host={Host}, Port={Port}, SecureSocketOption={SecureSocketOption}",
                options.Imap.Host,
                options.Imap.Port,
                options.Imap.SecureSocketOption);

            await imapClient.ConnectAsync(
                options.Imap.Host,
                options.Imap.Port,
                options.Imap.GetSecureSocketOptions());
            await imapClient.AuthenticateAsync(options.Imap.UserName, options.Imap.Password);

            receiveFolder = await GetOrCreateFolderAsync(options.Imap.Mailbox);
            await receiveFolder.OpenAsync(FolderAccess.ReadWrite);
            processedFolder = await GetOrCreateProcessedMailboxAsync(receiveFolder);

            logger.LogInformation(
                "Connected and prepared IMAP folders. Host={Host}, UserName={UserName}, Mailbox={Mailbox}, ProcessedMailbox={ProcessedMailbox}",
                options.Imap.Host,
                options.Imap.UserName,
                options.Imap.Mailbox,
                options.Processing.ProcessedMailbox);
        }
        finally
        {
            imapLock.Release();
        }
    }

    /// <summary>
    /// IMAPサーバーから正常に切断します。
    /// </summary>
    public async Task DisconnectAsync()
    {
        await imapLock.WaitAsync();
        try
        {
            if (imapClient?.IsConnected == true)
            {
                await imapClient.DisconnectAsync(true);
                logger.LogInformation("Disconnected from IMAP server.");
            }
        }
        finally
        {
            receiveFolder = null;
            processedFolder = null;
            imapClient?.Dispose();
            imapClient = null;
            imapLock.Release();
        }
    }

    /// <summary>
    /// 検索条件に一致する処理対象メールのUID一覧を取得します。
    /// </summary>
    public async Task<IReadOnlyList<UniqueId>> SearchTargetMessagesAsync(SearchQuery query, int maxMessages)
    {
        await imapLock.WaitAsync();
        try
        {
            IMailFolder folder = GetOpenedReceiveFolder();
            IList<UniqueId> uids = await folder.SearchAsync(query);
            List<UniqueId> targetUids = uids.Take(maxMessages).ToList();

            logger.LogInformation(
                "Found {MessageCount} target messages. Mailbox={Mailbox}",
                targetUids.Count,
                options.Imap.Mailbox);

            return targetUids;
        }
        finally
        {
            imapLock.Release();
        }
    }

    /// <summary>
    /// 指定されたUIDのメール本文と内部受信日時を取得し、受信メールリクエストを作成します。
    /// </summary>
    public async Task<ReceivedMailRequest> CreateRequestAsync(UniqueId uid)
    {
        await imapLock.WaitAsync();
        try
        {
            IMailFolder folder = GetOpenedReceiveFolder();
            MimeMessage message = await folder.GetMessageAsync(uid);
            IList<IMessageSummary> summary = await folder.FetchAsync([uid], MessageSummaryItems.InternalDate);

            ReceivedMailRequest request = ReceivedMailMapper.ToRequest(
                message,
                summary.FirstOrDefault()?.InternalDate);

            return request with { Uid = uid };
        }
        finally
        {
            imapLock.Release();
        }
    }

    /// <summary>
    /// 処理済みメールを設定されたメールボックスへ移動します。
    /// </summary>
    public async Task MoveToProcessedMailboxAsync(UniqueId uid, string messageId)
    {
        await imapLock.WaitAsync();
        try
        {
            IMailFolder folder = GetOpenedReceiveFolder();
            processedFolder ??= await GetOrCreateProcessedMailboxAsync(folder);
            await folder.MoveToAsync(uid, processedFolder);
        }
        finally
        {
            imapLock.Release();
        }

        logger.LogInformation(
            "Moved processed message. MessageId={MessageId}, DestinationMailbox={DestinationMailbox}",
            messageId,
            options.Processing.ProcessedMailbox);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        imapLock.Dispose();
    }

    private IMailFolder GetOpenedReceiveFolder()
    {
        if (receiveFolder?.IsOpen != true)
        {
            throw new InvalidOperationException("Receive mailbox is not open. Call ConnectAsync before operating mail folders.");
        }

        return receiveFolder;
    }

    private async Task<IMailFolder> GetOrCreateFolderAsync(string folderName)
    {
        try
        {
            return await imapClient!.GetFolderAsync(folderName);
        }
        catch (FolderNotFoundException)
        {
            if (imapClient!.PersonalNamespaces.Count == 0)
            {
                throw;
            }

            IMailFolder root = imapClient.GetFolder(imapClient.PersonalNamespaces[0]);
            return await root.CreateAsync(folderName, true);
        }
    }

    private async Task<IMailFolder> GetOrCreateProcessedMailboxAsync(IMailFolder folder)
    {
        try
        {
            return await folder.GetSubfolderAsync(options.Processing.ProcessedMailbox);
        }
        catch (FolderNotFoundException)
        {
            return await folder.CreateAsync(options.Processing.ProcessedMailbox, true);
        }
    }
}
