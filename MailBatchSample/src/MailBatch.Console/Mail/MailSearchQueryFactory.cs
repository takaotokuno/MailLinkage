using MailBatch.Console.Options;
using MailKit.Search;

namespace MailBatch.Console.Mail;

internal static class MailSearchQueryFactory
{
    /// <summary>
    /// メール検索設定から、対象メッセージを絞り込む検索クエリを作成します。
    /// </summary>
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

        /// <summary>
        /// 現在の検索クエリに指定された条件を追加します。
        /// </summary>
        void AddFilter(SearchQuery filter)
        {
            query = query is null ? filter : query.And(filter);
        }
    }
}
