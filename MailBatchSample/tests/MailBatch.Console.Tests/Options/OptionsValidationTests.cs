using MailBatch.Console.Options;
using MailKit.Security;
using Xunit;

namespace MailBatch.Console.Tests.Options;

public sealed class OptionsValidationTests
{
    /// <summary>
    /// 状態: APIのBaseUrlに相対URIが設定されている。
    /// 振る舞い: 絶対URIではないため、Api:BaseUrlの検証エラーを送出する。
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
    /// 状態: Batchのログ出力先ディレクトリが空白で設定されている。
    /// 振る舞い: 必須項目が未設定のため、Batch:LogDirectoryの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void BatchOptionsValidate_WithBlankLogDirectory_ThrowsInvalidOperationException()
    {
        BatchOptions options = new()
        {
            LogDirectory = " "
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Batch:LogDirectory is required.", exception.Message);
    }


    /// <summary>
    /// 状態: Batchのログ保管期間に0が設定されている。
    /// 振る舞い: 正の値ではないため、Batch:LogRetentionDaysの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void BatchOptionsValidate_WithZeroLogRetentionDays_ThrowsInvalidOperationException()
    {
        BatchOptions options = new()
        {
            LogRetentionDays = 0
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Batch:LogRetentionDays must be greater than 0.", exception.Message);
    }

    /// <summary>
    /// 状態: IMAP接続に必要な値とSocketOptionsがすべて設定されている。
    /// 振る舞い: 検証エラーを送出しない。
    /// </summary>
    [Fact]
    public void ImapOptionsValidate_WithValidOptions_DoesNotThrow()
    {
        ImapOptions options = CreateValidImapOptions(SecureSocketOptions.StartTls);

        Exception? exception = Record.Exception(options.Validate);

        Assert.Null(exception);
    }

    /// <summary>
    /// 状態: IMAP接続設定のSocketOptionsに不正な値が設定されている。
    /// 振る舞い: MailKitで扱える値ではないため、Imap:SocketOptionsの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void ImapOptionsValidate_WithInvalidSocketOptions_ThrowsInvalidOperationException()
    {
        ImapOptions options = CreateValidImapOptions((SecureSocketOptions)(-1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Imap:SocketOptions must be a valid SecureSocketOptions value.", exception.Message);
    }

    /// <summary>
    /// 状態: メール検索の最大取得件数に0が設定されている。
    /// 振る舞い: 正の値ではないため、MailSearch:MaxMessagesの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void MailSearchOptionsValidate_WithNonPositiveMaxMessages_ThrowsInvalidOperationException()
    {
        MailSearchOptions options = new()
        {
            MaxMessages = 0
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("MailSearch:MaxMessages must be greater than 0.", exception.Message);
    }

    /// <summary>
    /// 状態: 処理済みメールボックス名が空文字で設定されている。
    /// 振る舞い: 必須項目が未設定のため、Processing:ProcessedMailboxの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void ProcessingOptionsValidate_WithBlankProcessedMailbox_ThrowsInvalidOperationException()
    {
        ProcessingOptions options = new()
        {
            ProcessedMailbox = ""
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Processing:ProcessedMailbox is required.", exception.Message);
    }


    /// <summary>
    /// 状態: エラーメールボックス名が空白で設定されている。
    /// 振る舞い: 必須項目が未設定のため、Processing:ErrorMailboxの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void ProcessingOptionsValidate_WithBlankErrorMailbox_ThrowsInvalidOperationException()
    {
        ProcessingOptions options = new()
        {
            ErrorMailbox = " "
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Processing:ErrorMailbox is required.", exception.Message);
    }

    /// <summary>
    /// 状態: リクエストキューの上限に0が設定されている。
    /// 振る舞い: 正の値ではないため、Processing:RequestQueueCapacityの検証エラーを送出する。
    /// </summary>
    [Fact]
    public void ProcessingOptionsValidate_WithNonPositiveRequestQueueCapacity_ThrowsInvalidOperationException()
    {
        ProcessingOptions options = new()
        {
            RequestQueueCapacity = 0
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Equal("Processing:RequestQueueCapacity must be greater than 0.", exception.Message);
    }

    /// <summary>
    /// 状態: 通知メールテンプレートの本文が空白で設定されている。
    /// 振る舞い: テンプレート本文が必須項目のため、Notification:Templates:0:Bodyの検証エラーを送出する。
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

    private static ImapOptions CreateValidImapOptions(SecureSocketOptions socketOptions)
    {
        return new ImapOptions
        {
            Host = "imap.example.local",
            Port = 993,
            SocketOptions = socketOptions,
            UserName = "user",
            Password = "password",
            Mailbox = "INBOX"
        };
    }
}
