using MailBatch.Console.ReceivedMails;
using MailKit.Search;

namespace MailBatch.Console.Infrastructure;

/// <summary>
/// アプリケーション層のメール検索条件をMailKitの検索クエリへ変換します。
/// </summary>
internal static class MailKitSearchQueryMapper
{
    public static SearchQuery ToSearchQuery(MailSearchCondition condition)
    {
        SearchQuery? query = null;

        if (!string.IsNullOrWhiteSpace(condition.SubjectContains))
        {
            AddFilter(SearchQuery.SubjectContains(condition.SubjectContains));
        }

        if (!string.IsNullOrWhiteSpace(condition.From))
        {
            AddFilter(SearchQuery.FromContains(condition.From));
        }

        if (condition.DeliveredAfter is DateTime deliveredAfter)
        {
            AddFilter(SearchQuery.DeliveredAfter(deliveredAfter));
        }

        return query ?? SearchQuery.All;

        void AddFilter(SearchQuery filter)
        {
            query = query is null ? filter : query.And(filter);
        }
    }
}
