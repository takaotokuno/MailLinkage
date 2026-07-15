using MailBatch.Console.Options;
using MailKit.Security;
using Xunit;

namespace MailBatch.Console.Tests.Options;

public sealed class OptionsValidationTests
{
    /// <summary>
    /// 状態: API ベース URL に相対 URL を設定する。
    /// 振る舞い: API オプション検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void ApiOptionsValidate_WithRelativeBaseUrl_ThrowsInvalidOperationException()
    {
        ApiOptions options = new()
        {
            BaseUrl = new Uri("/api", UriKind.Relative),
            Endpoint = "/received-mails"
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Api:BaseUrl must be an absolute URI.", exception.Message);
    }

    /// <summary>
    /// 状態: ログ出力ディレクトリに空白を設定する。
    /// 振る舞い: バッチオプション検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void BatchOptionsValidate_WithBlankLogDirectory_ThrowsInvalidOperationException()
    {
        BatchOptions options = new() { LogDirectory = " " };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Batch:LogDirectory is required.", exception.Message);
    }

    /// <summary>
    /// 状態: 有効な IMAP オプションに小文字のセキュアソケット設定を指定する。
    /// 振る舞い: 例外を投げず、大文字小文字を無視して設定を解釈する。
    /// </summary>
    [Fact]
    public void ImapOptionsValidate_WithValidOptions_DoesNotThrowAndParsesSecureSocketOptionIgnoringCase()
    {
        ImapOptions options = CreateValidImapOptions(secureSocketOption: "starttls");

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
        Assert.Equal(SecureSocketOptions.StartTls, options.GetSecureSocketOptions());
    }

    /// <summary>
    /// 状態: IMAP オプションに不正なセキュアソケット設定を指定する。
    /// 振る舞い: 検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void ImapOptionsValidate_WithInvalidSecureSocketOption_ThrowsInvalidOperationException()
    {
        ImapOptions options = CreateValidImapOptions(secureSocketOption: "invalid");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Imap:SecureSocketOption must be a valid SecureSocketOptions value.", exception.Message);
    }

    /// <summary>
    /// 状態: 最大取得件数に 0 以下を設定する。
    /// 振る舞い: メール検索オプション検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void MailSearchOptionsValidate_WithNonPositiveMaxMessages_ThrowsInvalidOperationException()
    {
        MailSearchOptions options = new() { MaxMessages = 0 };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("MailSearch:MaxMessages must be greater than 0.", exception.Message);
    }

    /// <summary>
    /// 状態: 処理済みメールボックスに空文字を設定する。
    /// 振る舞い: 処理オプション検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void ProcessingOptionsValidate_WithBlankProcessedMailbox_ThrowsInvalidOperationException()
    {
        ProcessingOptions options = new() { ProcessedMailbox = "" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Processing:ProcessedMailbox is required.", exception.Message);
    }

    /// <summary>
    /// 状態: 通知テンプレート本文に空白を設定する。
    /// 振る舞い: 通知テンプレートオプション検証で InvalidOperationException を投げる。
    /// </summary>
    [Fact]
    public void MailNotificationTemplateOptionsValidate_WithBlankBody_ThrowsInvalidOperationException()
    {
        MailNotificationTemplateOptions options = new()
        {
            Name = "RunStatus",
            Subject = "subject",
            Body = " "
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            options.Validate("Notification:Templates:0");
        });

        Assert.Equal("Notification:Templates:0:Body is required.", exception.Message);
    }

    private static ImapOptions CreateValidImapOptions(string secureSocketOption)
    {
        return new ImapOptions
        {
            Host = "imap.example.local",
            Port = 993,
            SecureSocketOption = secureSocketOption,
            UserName = "user",
            Password = "password",
            Mailbox = "INBOX"
        };
    }
}
