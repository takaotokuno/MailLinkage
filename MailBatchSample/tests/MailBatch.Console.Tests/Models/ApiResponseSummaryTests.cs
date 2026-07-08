using MailBatch.Console.Models;
using Xunit;

namespace MailBatch.Console.Tests.Models;

public sealed class ApiResponseSummaryTests
{
    // APIレスポンス本文に含まれる保存済みIDを、大文字小文字に依存せず抽出できることを確認する。
    [Theory]
    [InlineData("{\"id\":123}", "123")]
    [InlineData("{\"ID\" : 456, \"messageId\":\"<mail@example.com>\"}", "456")]
    public void ExtractSavedId_ReturnsIdValueWhenResponseContainsId(string responseBody, string expectedId)
    {
        string? savedId = ApiResponseSummary.ExtractSavedId(responseBody);

        Assert.Equal(expectedId, savedId);
    }

    // APIレスポンス本文が空、またはIDを含まない場合に null を返すことを確認する。
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"message\":\"created\"}")]
    public void ExtractSavedId_ReturnsNullWhenResponseDoesNotContainId(string responseBody)
    {
        string? savedId = ApiResponseSummary.ExtractSavedId(responseBody);

        Assert.Null(savedId);
    }

    // レスポンス概要が空白を正規化し、指定された最大長を超える場合は省略記号を付与して切り詰めることを確認する。
    [Fact]
    public void Summarize_NormalizesWhitespaceAndTruncatesLongText()
    {
        string summary = ApiResponseSummary.Summarize("  first\n\tsecond   third  ", maxLength: 12);

        Assert.Equal("first second...", summary);
    }

    // 空白のみのレスポンス本文は空文字の概要として扱うことを確認する。
    [Fact]
    public void Summarize_ReturnsEmptyStringForBlankText()
    {
        string summary = ApiResponseSummary.Summarize(" \n\t ");

        Assert.Equal(string.Empty, summary);
    }
}
