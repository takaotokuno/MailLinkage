using MailBatch.Console.ReceivedMails;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails;

public sealed class MailSearchConditionFactoryTests
{
    [Fact]
    public void Create_ReturnsEmptyConditionWhenNoFiltersAreConfigured()
    {
        MailSearchCondition condition = MailSearchConditionFactory.Create(new MailSearchOptions { SinceDays = null });

        Assert.Equal(MailSearchCondition.All, condition);
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

        MailSearchCondition condition = MailSearchConditionFactory.Create(options);

        Assert.Equal("Target", condition.SubjectContains);
        Assert.Equal("sender@example.local", condition.From);
        Assert.Equal(expectedDate, condition.DeliveredAfter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        MailSearchCondition condition = MailSearchConditionFactory.Create(new MailSearchOptions { SinceDays = sinceDays });

        Assert.Equal(MailSearchCondition.All, condition);
    }
}
