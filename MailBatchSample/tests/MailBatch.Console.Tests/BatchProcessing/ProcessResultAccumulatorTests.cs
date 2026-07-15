using MailBatch.Console.BatchProcessing;
using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing;

public sealed class ProcessResultAccumulatorTests
{
    /// <summary>
    /// 状態: 初期総件数を持つ処理結果集計に成功と失敗を追加する。
    /// 振る舞い: 総件数、成功件数、失敗件数を反映した結果を返す。
    /// </summary>
    [Fact]
    public void ToResult_ReturnsAccumulatedCountsWithInitialTotal()
    {
        ProcessResultAccumulator accumulator = new(total: 3);

        accumulator.IncrementSuccess();
        accumulator.IncrementSuccess();
        accumulator.IncrementFailure();
        ProcessResult result = accumulator.ToResult();

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
    }
}
