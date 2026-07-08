using System.Threading.Channels;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// Producer / Consumer 間で利用する非同期キューを生成します。
/// </summary>
internal interface IQueueFactory<T>
{
    Channel<T> CreateSingleReaderSingleWriterQueue();
}
