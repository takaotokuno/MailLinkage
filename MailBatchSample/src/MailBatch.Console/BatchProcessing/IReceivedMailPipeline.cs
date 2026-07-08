using MailBatch.Console.Models;
using MailKit;

namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// 対象メールの取得・加工からAPI送信までのパイプラインを実行します。
/// </summary>
internal interface IReceivedMailPipeline
{
    Task<ProcessResult> ProcessAsync(IReadOnlyList<UniqueId> targetUids);
}
