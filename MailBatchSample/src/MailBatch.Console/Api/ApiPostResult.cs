namespace MailBatch.Console.Api;

/// <summary>
/// APIへのPOST結果をアプリケーション層向けに表現します。
/// </summary>
internal sealed record ApiPostResult(bool IsSuccess, int StatusCode, string ResponseBody);
