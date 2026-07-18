namespace MailBatch.Console.Options;

/// <summary>
/// 連携先APIの接続設定を保持します。
/// </summary>
internal sealed class ApiOptions
{
    public Uri? BaseUrl
    {
        get; init;
    }
    public string Endpoint { get; init; } = "/api/received-mails";
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryCount { get; init; } = 3;
    public int RetryDelaySeconds { get; init; } = 2;

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
