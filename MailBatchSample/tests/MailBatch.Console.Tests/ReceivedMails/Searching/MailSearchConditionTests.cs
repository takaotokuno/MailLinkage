using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.Searching;

namespace MailBatch.Console.Tests.ReceivedMails.Searching;

/// <summary>
/// メール検索条件の生成規則を検証します。
/// </summary>
public sealed class MailSearchConditionTests
{
    /// <summary>
    /// 呼び出し元のオフセットによらず、UTCの日付境界から検索開始日時が計算されることを確認する。
    /// </summary>
    /// <remarks>
    /// 前提・入力: UTCでは前日となるJSTの現在日時と、2日前から検索する設定を渡す。<br/>
    /// 期待結果: UTCへ変換した日付の午前0時から2日前が、UTCオフセット付きで設定される。<br/>
    /// 検知したい異常: 呼び出し元のローカル日付を基準にすることで検索範囲が1日ずれる不具合、またはUTCオフセットの欠落。
    /// </remarks>
    [Fact]
    public void FromOptions_WithSinceDays_CalculatesDeliveredAfterFromUtcDate()
    {
        MailSearchOptions options = new()
        {
            SinceDays = 2,
        };
        DateTimeOffset now = new(2026, 8, 2, 2, 30, 0, TimeSpan.FromHours(9));

        MailSearchCondition condition = MailSearchCondition.FromOptions(options, now);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
            condition.DeliveredAfter);
    }
}
