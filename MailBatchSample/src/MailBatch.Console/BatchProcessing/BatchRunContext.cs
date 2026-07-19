namespace MailBatch.Console.BatchProcessing;

/// <summary>
/// バッチ実行単位で共有する実行コンテキストを表します。
/// </summary>
internal sealed record BatchRunContext(string RunId);
