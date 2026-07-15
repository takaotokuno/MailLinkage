using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Searching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Searching;

public sealed class MailSearchConditionTests
{
    /// <summary>
    /// 状態: メール検索オプションに検索条件が設定されていない。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Create_ReturnsEmptyConditionWhenNoFiltersAreConfigured()
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(new MailSearchOptions { SinceDays = null }, DateTime.UtcNow);

        Assert.Null(condition.SubjectContains);
        Assert.Null(condition.From);
        Assert.Null(condition.DeliveredAfter);
    }

    /// <summary>
    /// 状態: メール検索オプションに件名、差出人、経過日数の条件が設定されている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void Create_IncludesSubjectFromAndSinceFiltersWhenConfigured()
    {
        MailSearchOptions options = new()
        {
            SubjectContains = "Target",
            From = "sender@example.local",
            SinceDays = 3
        };
        DateTime utcNow = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        DateTime expectedDate = utcNow.Date.AddDays(-3);

        MailSearchCondition condition = MailSearchCondition.FromOptions(options, utcNow);

        Assert.Equal("Target", condition.SubjectContains);
        Assert.Equal("sender@example.local", condition.From);
        Assert.Equal(expectedDate, condition.DeliveredAfter);
    }

    /// <summary>
    /// 状態: メール検索オプションの経過日数に0以下の値が設定されている。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(new MailSearchOptions { SinceDays = sinceDays }, DateTime.UtcNow);

        Assert.Null(condition.SubjectContains);
        Assert.Null(condition.From);
        Assert.Null(condition.DeliveredAfter);
    }
}
