using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Searching;

namespace MailBatch.Console.Tests.ReceivedMails.Searching;

/// <summary>
/// メール検索条件の生成規則を検証します。
/// </summary>
public sealed class MailSearchConditionTests
{
    /// <summary>
    /// 日数指定がある場合に、UTCの日付境界から検索開始日時が計算されることを検証します。
    /// </summary>
    [Fact]
    public void FromOptions_WithSinceDays_CalculatesDeliveredAfterFromUtcDate()
    {
        MailSearchOptions options = new()
        {
            SinceDays = 2,
        };
        DateTimeOffset now = new(2026, 8, 2, 2, 30, 0, TimeSpan.FromHours(9));

        MailSearchCondition condition = MailSearchCondition.FromOptions(options, now);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
            condition.DeliveredAfter);
    }

    /// <summary>
    /// 日数指定がない場合に、検索開始日時が設定されないことを検証します。
    /// </summary>
    [Fact]
    public void FromOptions_WithoutSinceDays_DoesNotSetDeliveredAfter()
    {
        MailSearchOptions options = new()
        {
            SinceDays = null,
        };

        MailSearchCondition condition = MailSearchCondition.FromOptions(
            options,
            DateTimeOffset.UtcNow);

        Assert.Null(condition.DeliveredAfter);
    }
}
