namespace MailBatch.Console.Options;

/// <summary>
/// 連携先APIの接続設定を保持します。
/// </summary>
internal sealed class ApiOptions
{
    private const int DEFAULT_TIMEOUT_SECONDS = 30;
    private const int DEFAULT_RETRY_COUNT = 3;
    private const int DEFAULT_RETRY_DELAY_SECONDS = 2;

    public Uri? BaseUrl
    {
        get; init;
    }
    public string Endpoint { get; init; } = "/api/received-mails";
    public int TimeoutSeconds { get; init; } = DEFAULT_TIMEOUT_SECONDS;
    public int RetryCount { get; init; } = DEFAULT_RETRY_COUNT;
    public int RetryDelaySeconds { get; init; } = DEFAULT_RETRY_DELAY_SECONDS;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
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
