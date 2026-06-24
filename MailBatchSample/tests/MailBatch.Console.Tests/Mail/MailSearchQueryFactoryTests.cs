using Xunit;
using MailBatch.Console.Mail;
using MailBatch.Console.Options;

namespace MailBatch.Console.Tests.Mail;

public sealed class MailSearchQueryFactoryTests
{
    [Fact]
    public void Create_ReturnsAllQueryWhenNoFiltersAreConfigured()
    {
        var query = MailSearchQueryFactory.Create(new MailSearchOptions());

        Assert.Equal("ALL", query.ToString());
    }

    [Fact]
    public void Create_IncludesUnreadSubjectAndSinceFiltersWhenConfigured()
    {
        var options = new MailSearchOptions
        {
            UnreadOnly = true,
            SubjectContains = "Target",
            SinceDays = 3
        };
        var expectedDate = DateTime.UtcNow.Date.AddDays(-3).ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);

        var query = MailSearchQueryFactory.Create(options);
        var queryText = query.ToString();

        Assert.Contains("NOT SEEN", queryText);
        Assert.Contains("SUBJECT Target", queryText);
        Assert.Contains($"SINCE {expectedDate}", queryText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        var query = MailSearchQueryFactory.Create(new MailSearchOptions { SinceDays = sinceDays });

        Assert.Equal("ALL", query.ToString());
    }
}
