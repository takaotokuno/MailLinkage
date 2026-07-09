using System.Globalization;
using MailBatch.Console.Infrastructure;
using MailBatch.Console.ReceivedMails;
using MailKit.Search;
using Xunit;

namespace MailBatch.Console.Tests.Infrastructure;

public sealed class MailKitSearchQueryMapperTests
{
    [Fact]
    public void ToSearchQuery_ReturnsAllQueryWhenNoFiltersAreConfigured()
    {
        SearchQuery query = MailKitSearchQueryMapper.ToSearchQuery(MailSearchCondition.All);

        Assert.Equal(SearchQuery.All, query);
    }

    [Fact]
    public void ToSearchQuery_IncludesSubjectFromAndDeliveredAfterFiltersWhenConfigured()
    {
        DateTime deliveredAfter = DateTime.UtcNow.Date.AddDays(-3);
        MailSearchCondition condition = new("Target", "sender@example.local", deliveredAfter);

        SearchQuery query = MailKitSearchQueryMapper.ToSearchQuery(condition);
        IReadOnlyCollection<string> terms = GetSearchTerms(query);

        Assert.Contains("SubjectContains", terms);
        Assert.Contains("FromContains", terms);
        Assert.Contains("DeliveredAfter", terms);
        Assert.Contains("Target", GetSearchValues(query));
        Assert.Contains("sender@example.local", GetSearchValues(query));
        Assert.Contains(deliveredAfter.ToString(CultureInfo.InvariantCulture), GetSearchValues(query));
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
