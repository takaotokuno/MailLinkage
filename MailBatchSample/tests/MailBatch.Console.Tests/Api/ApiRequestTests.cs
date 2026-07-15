using System.Text.Json;
using MailBatch.Console.Api;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiRequestTests
{
    /// <summary>
    /// 状態: API リクエストにメッセージを設定する。
    /// 振る舞い: JSON シリアライズ結果に Message だけが含まれる。
    /// </summary>
    [Fact]
    public void Serialize_IncludesMessageOnly()
    {
        ApiRequest request = new("linked message");

        string json = JsonSerializer.Serialize(request);

        Assert.Equal(/*lang=json,strict*/ "{\"Message\":\"linked message\"}", json);
    }
}
