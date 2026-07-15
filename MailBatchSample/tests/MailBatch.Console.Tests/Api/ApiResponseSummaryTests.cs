using MailBatch.Console.Api;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiResponseSummaryTests
{
    /// <summary>
    /// 状態: APIレスポンス本文に含まれる保存済みIDを、大文字小文字に依存せず抽出できる。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ "{\"id\":123}", "123")]
    [InlineData(/*lang=json,strict*/ "{\"ID\" : 456, \"messageId\":\"<mail@example.com>\"}", "456")]
    public void ExtractSavedId_ReturnsIdValueWhenResponseContainsId(string responseBody, string expectedId)
    {
        string? savedId = ApiResponseSummary.ExtractSavedId(responseBody);

        Assert.Equal(expectedId, savedId);
    }

    /// <summary>
    /// 状態: APIレスポンス本文が空、またはIDを含まない場合に null を返す。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(/*lang=json,strict*/ "{\"message\":\"created\"}")]
    public void ExtractSavedId_ReturnsNullWhenResponseDoesNotContainId(string responseBody)
    {
        string? savedId = ApiResponseSummary.ExtractSavedId(responseBody);

        Assert.Null(savedId);
    }

    /// <summary>
    /// 状態: レスポンス概要が空白を正規化し、指定された最大長を超える場合は省略記号を付与して切り詰める。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Summarize_NormalizesWhitespaceAndTruncatesLongText()
    {
        string summary = ApiResponseSummary.Summarize("  first\n\tsecond   third  ", maxLength: 12);

        Assert.Equal("first second...", summary);
    }

    /// <summary>
    /// 状態: 空白のみのレスポンス本文は空文字の概要として扱う。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Summarize_ReturnsEmptyStringForBlankText()
    {
        string summary = ApiResponseSummary.Summarize(" \n\t ");

        Assert.Equal(string.Empty, summary);
    }
}
