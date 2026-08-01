using MailBatch.Console.BatchProcessing.History;
using MailBatch.Console.Options;
using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing.History;

public sealed class SqliteBatchRunHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"batch-run-history-{Guid.NewGuid():N}");

    // 目的: バッチ実行履歴の保存内容と新しい順の取得を確認する。
    // 前提・入力: 結果の異なる新旧2件の実行履歴をSQLiteへ保存する。
    // 期待結果: 取得上限内の履歴が新しい順で返り、各終了コードと集計値が保存値に一致する。
    // 検知したい異常: 履歴の並び順または実行結果の永続化が崩れる不具合。
    [Fact]
    public async Task AddAsync_ThenGetRecentAsync_ReturnsNewestRunsWithResults()
    {
        SqliteBatchRunHistoryStore store = new(new BatchOptions { LogDirectory = _directory });
        DateTimeOffset now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
        BatchRunHistory older = new("run-old", now.AddHours(-3), now.AddHours(-2), 0, 10, 9, 1, 0, null, null);
        BatchRunHistory newer = new("run-new", now.AddHours(-2), now, 1, 5, 3, 1, 1, "Failure", "Processing");

        await store.AddAsync(older);
        await store.AddAsync(newer);

        BatchRunHistory actual = Assert.Single(await store.GetRecentAsync(1));
        Assert.Equal(newer, actual);
        Assert.Equal(TimeSpan.FromHours(2), actual.Duration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
