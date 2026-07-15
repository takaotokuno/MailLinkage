using System.Text.Json;
using MailBatch.Console.Api;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiRequestTests
{
    // API に送信する JSON が Message のみを含むことを確認する。
    [Fact]
    public void Serialize_IncludesMessageOnly()
    {
        ApiRequest request = new("linked message");

        string json = JsonSerializer.Serialize(request);

        Assert.Equal(/*lang=json,strict*/ "{\"Message\":\"linked message\"}", json);
    }
}
