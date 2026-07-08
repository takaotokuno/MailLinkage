namespace MailBatch.Console.Options;

internal sealed class ApiOptions
{
    public Uri? BaseUrl { get; init; }
    public string Endpoint { get; init; } = "/api/received-mails";
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// 必須項目と値の範囲を検証します。
    /// </summary>
    public void Validate()
    {
        OptionValidation.Require(Endpoint, "Api:Endpoint");
        OptionValidation.RequirePositive(TimeoutSeconds, "Api:TimeoutSeconds");
        if (BaseUrl is null || !BaseUrl.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Api:BaseUrl must be an absolute URI.");
        }
    }

}
