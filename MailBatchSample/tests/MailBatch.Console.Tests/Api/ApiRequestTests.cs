using System.Text.Json;
using MailBatch.Console.Api;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiRequestTests
{
    /// <summary>
    /// 状態: API に送信する JSON が Message のみを含む。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Serialize_IncludesMessageOnly()
    {
        ApiRequest request = new("linked message");

        string json = JsonSerializer.Serialize(request);

        Assert.Equal(/*lang=json,strict*/ "{\"Message\":\"linked message\"}", json);
    }
}
