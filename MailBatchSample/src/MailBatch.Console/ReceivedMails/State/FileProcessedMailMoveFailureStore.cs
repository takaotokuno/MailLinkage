using System.Text.Json;
using System.Text.Json.Serialization;
using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.State;

internal interface IProcessedMailMoveFailureStore
{
    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

internal sealed class FileProcessedMailMoveFailureStore(
    BatchOptions batchOptions,
    ILogger<FileProcessedMailMoveFailureStore> logger) : IProcessedMailMoveFailureStore
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

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
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            return records.Contains(MailMoveFailureRecord.Processed(mailId));
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    public async Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => await AddAsync(
        MailMoveFailureRecord.Processed(mailId),
        "Recorded processed mailbox move failure. MailId={MailId}",
        cancellationToken);

    public async Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => await AddAsync(
        MailMoveFailureRecord.Error(mailId),
        "Recorded error mailbox move failure. MailId={MailId}",
        cancellationToken);

    private async Task AddAsync(MailMoveFailureRecord record, string logMessage, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            if (records.Add(record))
            {
                await SaveAsync(records, cancellationToken);
                logger.LogWarning(logMessage, record.MailId);
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
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            if (records.Remove(MailMoveFailureRecord.Processed(mailId)))
            {
                await SaveAsync(records, cancellationToken);
                logger.LogInformation("Cleared processed mailbox move failure record. MailId={MailId}", mailId);
            }
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    private async Task<HashSet<MailMoveFailureRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        string storePath = StorePath;
        if (!File.Exists(storePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(storePath);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        HashSet<MailMoveFailureRecord> records = [];

        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt32(out uint legacyUid))
            {
                _ = records.Add(MailMoveFailureRecord.Processed(new ReceivedMailId(legacyUid, 0)));
                continue;
            }

            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(nameof(ReceivedMailId.Uid), out JsonElement uidElement)
                && element.TryGetProperty(nameof(ReceivedMailId.UidValidity), out JsonElement uidValidityElement)
                && uidElement.TryGetUInt32(out uint uid)
                && uidValidityElement.TryGetUInt32(out uint uidValidity))
            {
                MailMoveFailureDestination destination = TryGetDestination(element, out MailMoveFailureDestination parsedDestination)
                    ? parsedDestination
                    : MailMoveFailureDestination.Processed;
                _ = records.Add(new MailMoveFailureRecord(new ReceivedMailId(uid, uidValidity), destination));
            }
        }

        return records;
    }

    private static bool TryGetDestination(JsonElement element, out MailMoveFailureDestination destination)
    {
        destination = MailMoveFailureDestination.Processed;
        if (!element.TryGetProperty(nameof(MailMoveFailureRecord.Destination), out JsonElement destinationElement)
            || destinationElement.ValueKind != JsonValueKind.String)
        {
            return destinationElement.ValueKind == JsonValueKind.Number
                && destinationElement.TryGetInt32(out int rawDestination)
                && Enum.IsDefined(typeof(MailMoveFailureDestination), rawDestination)
                && TryConvertDestination(rawDestination, out destination);
        }

        return Enum.TryParse(destinationElement.GetString(), ignoreCase: true, out destination);
    }

    private static bool TryConvertDestination(int rawDestination, out MailMoveFailureDestination destination)
    {
        destination = (MailMoveFailureDestination)rawDestination;
        return true;
    }

    private async Task SaveAsync(HashSet<MailMoveFailureRecord> records, CancellationToken cancellationToken)
    {
        string storePath = StorePath;
        _ = Directory.CreateDirectory(Path.GetDirectoryName(storePath) ?? ".");
        string temporaryPath = storePath + ".tmp";
        MailMoveFailureRecord[] sortedRecords = [.. records.OrderBy(record => { return record.MailId.UidValidity; }).ThenBy(record => { return record.MailId.Uid; }).ThenBy(record => { return record.Destination; })];

        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, sortedRecords, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, storePath, overwrite: true);
    }

    private readonly record struct MailMoveFailureRecord(ReceivedMailId MailId, MailMoveFailureDestination Destination)
    {
        public uint Uid => MailId.Uid;

        public uint UidValidity => MailId.UidValidity;

        public static MailMoveFailureRecord Processed(ReceivedMailId mailId) => new(mailId, MailMoveFailureDestination.Processed);

        public static MailMoveFailureRecord Error(ReceivedMailId mailId) => new(mailId, MailMoveFailureDestination.Error);
    }

    private enum MailMoveFailureDestination
    {
        Processed,
        Error
    }
}
