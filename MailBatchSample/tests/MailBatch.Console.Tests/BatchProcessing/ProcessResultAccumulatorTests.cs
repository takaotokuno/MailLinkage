using Xunit;

namespace MailBatch.Console.Tests.BatchProcessing;

public sealed class ProcessResultAccumulatorTests
{
    /// <summary>
    /// 状態: 成功・形式不正・API失敗の加算結果と初期Totalが、確定結果へ反映される。
    /// 振る舞い: 期待される結果を返す。
    /// </summary>
    [Fact]
    public void ToResult_ReturnsAccumulatedCountsWithInitialTotal()
    {
        ProcessResultAccumulator accumulator = new(total: 3);

        accumulator.IncrementSuccess();
        accumulator.IncrementSuccess();
        accumulator.IncrementInvalidFormat();
        accumulator.IncrementApiFailure();
        ProcessResult result = accumulator.ToResult();

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.InvalidFormat);
        Assert.Equal(1, result.ApiFailed);
        Assert.Equal(2, result.Failed);
    }
}
