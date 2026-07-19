using MailKit.Net.Imap;

namespace MailBatch.Console.ReceivedMails.Imap;

/// <summary>
/// IMAPサーバーとの接続ライフサイクルを管理します。
/// </summary>
internal interface IImapConnection : IAsyncDisposable
{
    ImapClient Client
    {
        get;
    }

    bool IsConnected
    {
        get;
    }

    /// <summary>
    /// IMAPサーバーへ接続して認証します。
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// IMAPサーバーとの接続を切断します。
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
