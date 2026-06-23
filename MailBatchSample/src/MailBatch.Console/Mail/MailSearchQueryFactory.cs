using MailBatch.Console.Options;
using MailKit.Search;

namespace MailBatch.Console.Mail;

internal static class MailSearchQueryFactory
{
    public static SearchQuery Create(MailSearchOptions options)
    {
        var query = SearchQuery.All;

        if (options.UnreadOnly)
        {
            query = query.And(SearchQuery.NotSeen);
        }

        if (!string.IsNullOrWhiteSpace(options.SubjectContains))
        {
            query = query.And(SearchQuery.SubjectContains(options.SubjectContains));
        }

        if (options.SinceDays is > 0)
        {
            query = query.And(SearchQuery.DeliveredAfter(DateTime.UtcNow.Date.AddDays(-options.SinceDays.Value)));
        }

        return query;
    }
}
