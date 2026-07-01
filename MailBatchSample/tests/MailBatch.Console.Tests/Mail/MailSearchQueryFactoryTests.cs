using System.Globalization;
using Xunit;
using MailBatch.Console.Mail;
using MailBatch.Console.Options;
using MailKit.Search;

namespace MailBatch.Console.Tests.Mail;

public sealed class MailSearchQueryFactoryTests
{
    // 検索条件が設定されていない場合に全件検索クエリを返すことを確認する。
    [Fact]
    public void Create_ReturnsAllQueryWhenNoFiltersAreConfigured()
    {
        var query = MailSearchQueryFactory.Create(new MailSearchOptions { UnreadOnly = false, SinceDays = null });

        Assert.Equal(SearchQuery.All, query);
    }

    // 未読、件名、指定日数以降の各条件が設定された場合に検索クエリへ含まれることを確認する。
    [Fact]
    public void Create_IncludesUnreadSubjectAndSinceFiltersWhenConfigured()
    {
        var options = new MailSearchOptions
        {
            UnreadOnly = true,
            SubjectContains = "Target",
            SinceDays = 3
        };
        var expectedDate = DateTime.UtcNow.Date.AddDays(-3);

        var query = MailSearchQueryFactory.Create(options);
        var terms = GetSearchTerms(query);

        Assert.Contains("NotSeen", terms);
        Assert.Contains("SubjectContains", terms);
        Assert.Contains("DeliveredAfter", terms);
        Assert.Contains("Target", GetSearchValues(query));
        Assert.Contains(expectedDate.ToString(CultureInfo.InvariantCulture), GetSearchValues(query));
    }

    // SinceDays が 0 以下の場合に日付フィルターを無視して全件検索クエリを返すことを確認する。
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        var query = MailSearchQueryFactory.Create(new MailSearchOptions { UnreadOnly = false, SinceDays = sinceDays });

        Assert.Equal(SearchQuery.All, query);
    }

    private static IReadOnlyCollection<string> GetSearchTerms(SearchQuery query)
    {
        var terms = new List<string>();
        Visit(query, q =>
        {
            var term = q.GetType().GetProperty("Term")?.GetValue(q)?.ToString();
            if (!string.IsNullOrWhiteSpace(term))
            {
                terms.Add(term);
            }
        });

        return terms;
    }

    private static IReadOnlyCollection<string> GetSearchValues(SearchQuery query)
    {
        var values = new List<string>();
        Visit(query, q =>
        {
            foreach (var property in q.GetType().GetProperties())
            {
                var value = property.GetValue(q);
                if (value is string text)
                {
                    values.Add(text);
                }
                else if (value is DateTime dateTime)
                {
                    values.Add(dateTime.ToString(CultureInfo.InvariantCulture));
                }
            }
        });

        return values;
    }

    private static void Visit(SearchQuery query, Action<SearchQuery> visitor)
    {
        visitor(query);

        foreach (var propertyName in new[] { "Left", "Right", "Operand" })
        {
            if (query.GetType().GetProperty(propertyName)?.GetValue(query) is SearchQuery child)
            {
                Visit(child, visitor);
            }
        }
    }
}
