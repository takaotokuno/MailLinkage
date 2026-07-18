using System.Text.Json;
using System.Text.Json.Serialization;
using MailBatch.Console.Options;
using Microsoft.Extensions.Logging;

namespace MailBatch.Console.ReceivedMails.State;

/// <summary>
/// メール移動失敗情報を永続化し、次回実行時の再処理に利用できるようにします。
/// </summary>
internal interface IProcessedMailMoveFailureStore
{
    Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task AddErrorMoveFailureAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);

    Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default);

    Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default);
}

/// <summary>
/// メール移動に失敗したメールIDと移動先を表します。
/// </summary>
internal readonly record struct MailMoveFailure(ReceivedMailId MailId, MailMoveFailureDestination Destination);

/// <summary>
/// メール移動失敗時に目標としていた移動先を表します。
/// </summary>
internal enum MailMoveFailureDestination
{
    Processed,
    Error
}

/// <summary>
/// メール移動失敗情報をJSONファイルへ保存します。
/// </summary>
internal sealed class FileProcessedMailMoveFailureStore(
    BatchOptions batchOptions,
    ILogger<FileProcessedMailMoveFailureStore> logger) : IProcessedMailMoveFailureStore
{
    // ファイルの読み書きを直列化し、同一プロセス内の並行更新で失敗記録が失われることを防ぎます。
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

    public async Task<IReadOnlyList<MailMoveFailure>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            return [.. records.Select(record => { return new MailMoveFailure(record.MailId, record.Destination); })];
        }
        finally
        {
            _ = _semaphore.Release();
        }
    }

    public async Task<bool> ContainsAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            return records.Any(record =>
            {
                return record.MailId == mailId;
            });
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

    public async Task RemoveAsync(MailMoveFailure failure, CancellationToken cancellationToken = default) => await RemoveAsync(new MailMoveFailureRecord(failure.MailId, failure.Destination), cancellationToken);

    public async Task RemoveAsync(ReceivedMailId mailId, CancellationToken cancellationToken = default) => await RemoveAsync(MailMoveFailureRecord.Processed(mailId), cancellationToken);

    private async Task RemoveAsync(MailMoveFailureRecord record, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            HashSet<MailMoveFailureRecord> records = await LoadAsync(cancellationToken);
            if (records.Remove(record))
            {
                await SaveAsync(records, cancellationToken);
                logger.LogInformation("Cleared mailbox move failure record. MailId={MailId}, Destination={Destination}", record.MailId, record.Destination);
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
            // 旧形式の失敗記録も読み込めるようにし、バージョンアップ直後に再移動対象が欠落することを防ぎます。
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
        return !element.TryGetProperty(nameof(MailMoveFailureRecord.Destination), out JsonElement destinationElement)
            ? false
            : destinationElement.ValueKind == JsonValueKind.String
            ? Enum.TryParse(destinationElement.GetString(), ignoreCase: true, out destination)
            : destinationElement.ValueKind == JsonValueKind.Number
            && destinationElement.TryGetInt32(out int rawDestination)
            && Enum.IsDefined(typeof(MailMoveFailureDestination), rawDestination)
            && TryConvertDestination(rawDestination, out destination);
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

        // 一時ファイルへ書き切ってから差し替え、保存途中の異常終了で本体ファイルが壊れるリスクを下げます。
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, sortedRecords, SerializerOptions, cancellationToken);
        }

        File.Move(temporaryPath, storePath, overwrite: true);
    }

    private readonly record struct MailMoveFailureRecord(ReceivedMailId MailId, MailMoveFailureDestination Destination)
    {
        public uint Uid
        {
            get
            {
                return MailId.Uid;
            }
        }

        public uint UidValidity
        {
            get
            {
                return MailId.UidValidity;
            }
        }

        public static MailMoveFailureRecord Processed(ReceivedMailId mailId) => new(mailId, MailMoveFailureDestination.Processed);

        public static MailMoveFailureRecord Error(ReceivedMailId mailId) => new(mailId, MailMoveFailureDestination.Error);
    }

}
