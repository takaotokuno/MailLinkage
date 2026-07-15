using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Searching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Searching;

public sealed class MailSearchConditionTests
{
    /// <summary>
    /// 状態: メール検索オプションに条件を設定しない。
    /// 振る舞い: 空の検索条件を作成する。
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
    /// 状態: 件名、送信者、起算日の検索オプションを設定する。
    /// 振る舞い: 各条件を含む検索条件を作成する。
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
    /// 状態: 起算日数に 0 以下を設定する。
    /// 振る舞い: 起算日条件を設定せず検索条件を作成する。
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
