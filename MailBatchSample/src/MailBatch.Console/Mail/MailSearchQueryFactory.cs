using MailBatch.Console.Options;
using MailKit.Search;

namespace MailBatch.Console.Mail;

internal static class MailSearchQueryFactory
{
    public static SearchQuery Create(MailSearchOptions options)
    {
        SearchQuery? query = null;

        if (options.UnreadOnly)
        {
            AddFilter(SearchQuery.NotSeen);
        }

        if (!string.IsNullOrWhiteSpace(options.SubjectContains))
        {
            AddFilter(SearchQuery.SubjectContains(options.SubjectContains));
        }

        if (options.SinceDays is > 0)
        {
            AddFilter(SearchQuery.DeliveredAfter(DateTime.UtcNow.Date.AddDays(-options.SinceDays.Value)));
        }

        return query ?? SearchQuery.All;

        void AddFilter(SearchQuery filter)
        {
            query = query is null ? filter : query.And(filter);
        }
    }
}
