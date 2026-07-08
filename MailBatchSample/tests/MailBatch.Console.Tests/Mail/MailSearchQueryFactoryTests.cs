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
        SearchQuery query = MailSearchQueryFactory.Create(new MailSearchOptions { SinceDays = null });

        Assert.Equal(SearchQuery.All, query);
    }

    // 件名、From、指定日数以降の各条件が設定された場合に検索クエリへ含まれることを確認する。
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

        SearchQuery query = MailSearchQueryFactory.Create(options);
        IReadOnlyCollection<string> terms = GetSearchTerms(query);

        Assert.DoesNotContain("NotSeen", terms);
        Assert.Contains("SubjectContains", terms);
        Assert.Contains("FromContains", terms);
        Assert.Contains("DeliveredAfter", terms);
        Assert.Contains("Target", GetSearchValues(query));
        Assert.Contains("sender@example.local", GetSearchValues(query));
        Assert.Contains(expectedDate.ToString(CultureInfo.InvariantCulture), GetSearchValues(query));
    }

    // SinceDays が 0 以下の場合に日付フィルターを無視して全件検索クエリを返すことを確認する。
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_IgnoresNonPositiveSinceDays(int sinceDays)
    {
        SearchQuery query = MailSearchQueryFactory.Create(new MailSearchOptions { SinceDays = sinceDays });

        Assert.Equal(SearchQuery.All, query);
    }

    private static IReadOnlyCollection<string> GetSearchTerms(SearchQuery query)
    {
        List<string> terms = new();
        Visit(query, q =>
        {
            string? term = q.GetType().GetProperty("Term")?.GetValue(q)?.ToString();
            if (!string.IsNullOrWhiteSpace(term))
            {
                terms.Add(term);
            }
        });

        return terms;
    }

    private static IReadOnlyCollection<string> GetSearchValues(SearchQuery query)
    {
        List<string> values = new();
        Visit(query, q =>
        {
            foreach (System.Reflection.PropertyInfo property in q.GetType().GetProperties())
            {
                object? value = property.GetValue(q);
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

        foreach (string? propertyName in new[] { "Left", "Right", "Operand" })
        {
            if (query.GetType().GetProperty(propertyName)?.GetValue(query) is SearchQuery child)
            {
                Visit(child, visitor);
            }
        }
    }
}
