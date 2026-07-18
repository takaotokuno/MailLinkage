using System.Text.Json;
using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.State;

internal interface IProcessedMailMoveFailureStore
{
    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

internal sealed class FileProcessedMailMoveFailureStore(
    BatchOptions batchOptions,
    ILogger<FileProcessedMailMoveFailureStore> logger) : IProcessedMailMoveFailureStore
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string StorePath
    {
        get
        {
            return Path.Combine(batchOptions.LogDirectory, "processed-mail-move-failures.json");
        }
    }

    public async Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<uint> mailIds = await LoadAsync(cancellationToken);
            return mailIds.Contains(mailId.Value);
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    public async Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<uint> mailIds = await LoadAsync(cancellationToken);
            if (mailIds.Add(mailId.Value))
            {
                await SaveAsync(mailIds, cancellationToken);
                logger.LogWarning("Recorded processed mailbox move failure. MailId={MailId}", mailId);
            }
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    public async Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<uint> mailIds = await LoadAsync(cancellationToken);
            if (mailIds.Remove(mailId.Value))
            {
                await SaveAsync(mailIds, cancellationToken);
                logger.LogInformation("Cleared processed mailbox move failure record. MailId={MailId}", mailId);
            }
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    private async Task<HashSet<uint>> LoadAsync(CancellationToken cancellationToken)
    {
        string storePath = StorePath;
        if (!File.Exists(storePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(storePath);
        uint[]? mailIds = await JsonSerializer.DeserializeAsync<uint[]>(stream, cancellationToken: cancellationToken);
        return mailIds?.ToHashSet() ?? [];
    }

    private async Task SaveAsync(HashSet<uint> mailIds, CancellationToken cancellationToken)
    {
        string storePath = StorePath;
        _ = Directory.CreateDirectory(Path.GetDirectoryName(storePath) ?? ".");
        string temporaryPath = storePath + ".tmp";
        uint[] sortedMailIds = [.. mailIds.Order()];

        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, sortedMailIds, cancellationToken: cancellationToken);
        }

        File.Move(temporaryPath, storePath, overwrite: true);
    }
}
