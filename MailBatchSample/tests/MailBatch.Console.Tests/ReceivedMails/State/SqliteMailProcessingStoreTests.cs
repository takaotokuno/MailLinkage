using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MailBatch.Console.Tests.ReceivedMails.State;

public sealed class SqliteMailProcessingStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"mail-processing-store-{Guid.NewGuid():N}");

    [Fact]
    public async Task Store_PersistsProcessedLedgerAndMoveFailuresAcrossInstances()
    {
        ReceivedMailId processedMailId = new(10, 1000);
        ReceivedMailId failedMailId = new(20, 1000);
        SqliteMailProcessingStore firstStore = CreateStore();

        await firstStore.RecordProcessedAsync(processedMailId);
        await firstStore.AddErrorMoveFailureAsync(failedMailId);

        SqliteMailProcessingStore reopenedStore = CreateStore();
        Assert.True(await reopenedStore.IsProcessedAsync(processedMailId));
        MailMoveFailure failure = Assert.Single(await reopenedStore.GetAllAsync());
        Assert.Equal(failedMailId, failure.MailId);
        Assert.Equal(MailMoveFailureDestination.Error, failure.Destination);

        await reopenedStore.RemoveAsync(failure);
        Assert.Empty(await reopenedStore.GetAllAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteMailProcessingStore CreateStore() => new(
        new BatchOptions { LogDirectory = _directory },
        NullLogger<SqliteMailProcessingStore>.Instance);
}
