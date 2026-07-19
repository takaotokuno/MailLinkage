namespace MailBatch.Console.Options;

/// <summary>
/// 連携先APIの接続設定を保持します。
/// </summary>
internal sealed class ApiOptions
{
    private const int DEFAULT_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_SECONDS = 2;

    /// <summary>連携先APIのベースURLを取得します。</summary>
    public Uri? BaseUrl
    {
        get; init;
    }
    /// <summary>API認証に使用するAPIキーを取得します。</summary>
    public string ApiKey { get; init; } = string.Empty;
    /// <summary>受信メールを送信するAPIエンドポイントの相対パスを取得します。</summary>
    public string Endpoint { get; init; } = "/api/received-mails";
    /// <summary>API要求のタイムアウト秒数を取得します。</summary>
    public int TimeoutSeconds { get; init; } = DEFAULT_TIMEOUT_SECONDS;
    /// <summary>API要求に失敗した場合の再試行回数を取得します。</summary>
    public int RetryCount { get; init; } = DEFAULT_RETRY_COUNT;
    /// <summary>API要求を再試行するまでの基準待機秒数を取得します。</summary>
    public int RetryDelaySeconds { get; init; } = DEFAULT_RETRY_DELAY_SECONDS;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(ApiKey, "Api:ApiKey");
        OptionValidation.Require(Endpoint, "Api:Endpoint");
        OptionValidation.RequirePositive(TimeoutSeconds, "Api:TimeoutSeconds");
        OptionValidation.RequireNonNegative(RetryCount, "Api:RetryCount");
        OptionValidation.RequirePositive(RetryDelaySeconds, "Api:RetryDelaySeconds");
        if (BaseUrl is null || !BaseUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Api:BaseUrl must be an absolute URI.");
        }
    }
}
