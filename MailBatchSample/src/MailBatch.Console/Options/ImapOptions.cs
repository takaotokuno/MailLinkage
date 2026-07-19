using MailKit.Security;

namespace MailBatch.Console.Options;

/// <summary>
/// IMAPサーバー接続と受信メールボックスに関する設定を保持します。
/// </summary>
internal sealed class ImapOptions
{
    private const int DEFAULT_PORT = 993;
    private const int DEFAULT_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_SECONDS = 2;

    /// <summary>IMAPサーバーのホスト名を取得します。</summary>
    public string Host { get; init; } = "";
    /// <summary>IMAPサーバーのポート番号を取得します。</summary>
    public int Port { get; init; } = DEFAULT_PORT;
    /// <summary>IMAP接続で使用するSSL/TLS方式を取得します。</summary>
    public SecureSocketOptions SocketOptions { get; init; } = SecureSocketOptions.SslOnConnect;
    /// <summary>IMAP認証に使用するユーザー名を取得します。</summary>
    public string UserName { get; init; } = "";
    /// <summary>IMAP認証に使用するパスワードを取得します。</summary>
    public string Password { get; init; } = "";
    /// <summary>処理対象の受信メールボックス名を取得します。</summary>
    public string Mailbox { get; init; } = "INBOX";
    /// <summary>IMAP接続に失敗した場合の再試行回数を取得します。</summary>
    public int RetryCount { get; init; } = DEFAULT_RETRY_COUNT;
    /// <summary>IMAP接続を再試行するまでの基準待機秒数を取得します。</summary>
    public int RetryDelaySeconds { get; init; } = DEFAULT_RETRY_DELAY_SECONDS;

    /// <summary>
    /// IMAP接続に必要な設定値を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(Host, "Imap:Host");
        OptionValidation.RequireRange(
            Port,
            OptionValidation.MINIMUM_NETWORK_PORT,
            OptionValidation.MAXIMUM_NETWORK_PORT,
            "Imap:Port");
        OptionValidation.Require(UserName, "Imap:UserName");
        OptionValidation.Require(Password, "Imap:Password");
        OptionValidation.Require(Mailbox, "Imap:Mailbox");
        OptionValidation.RequireNonNegative(RetryCount, "Imap:RetryCount");
        OptionValidation.RequirePositive(RetryDelaySeconds, "Imap:RetryDelaySeconds");

        OptionValidation.RequireDefined(SocketOptions, "Imap:SocketOptions");
    }
}
