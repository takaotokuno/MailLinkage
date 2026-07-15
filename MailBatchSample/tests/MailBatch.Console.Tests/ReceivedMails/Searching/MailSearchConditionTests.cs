using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Searching;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.Searching;

public sealed class MailSearchConditionTests
{
    [Fact]
    public void Create_ReturnsEmptyConditionWhenNoFiltersAreConfigured()
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(new MailSearchOptions { SinceDays = null });

        Assert.Null(condition.SubjectContains);
        Assert.Null(condition.From);
        Assert.Null(condition.DeliveredAfter);
    }

    [Fact]
    public void Create_IncludesSubjectFromAndSinceFiltersWhenConfigured()
    {
        MailSearchOptions options = new()
        {
            SubjectContains = "Target",
            From = "sender@example.local",
            SinceDays = 3
        };
        DateTime expectedDate = DateTime.UtcNow.Date.AddDays(-3);

        MailSearchCondition condition = MailSearchCondition.FromOptions(options);

        Assert.Equal("Target", condition.SubjectContains);
        Assert.Equal("sender@example.local", condition.From);
        Assert.Equal(expectedDate, condition.DeliveredAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        MailSearchCondition condition = MailSearchCondition.FromOptions(new MailSearchOptions { SinceDays = sinceDays });

        Assert.Null(condition.SubjectContains);
        Assert.Null(condition.From);
        Assert.Null(condition.DeliveredAfter);
    }
}
