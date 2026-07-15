using MailBatch.Console.Api;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiResponseSummaryTests
{
    /// <summary>
    /// 状態: API 応答本文に id プロパティが含まれる。
    /// 振る舞い: 保存 ID として id の値を抽出する。
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
    /// 状態: API 応答本文が空、空白、または id を含まない。
    /// 振る舞い: 保存 ID として null を返す。
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
    /// 状態: 改行やタブを含む長い文字列を要約する。
    /// 振る舞い: 空白を正規化し、指定した長さに切り詰める。
    /// </summary>
    [Fact]
    public void Summarize_NormalizesWhitespaceAndTruncatesLongText()
    {
        string summary = ApiResponseSummary.Summarize("  first\n\tsecond   third  ", maxLength: 12);

        Assert.Equal("first second...", summary);
    }

    /// <summary>
    /// 状態: 空白文字だけの文字列を要約する。
    /// 振る舞い: 空文字を返す。
    /// </summary>
    [Fact]
    public void Summarize_ReturnsEmptyStringForBlankText()
    {
        string summary = ApiResponseSummary.Summarize(" \n\t ");

        Assert.Equal(string.Empty, summary);
    }
}
