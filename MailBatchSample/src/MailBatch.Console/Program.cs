using MailBatch.Console.BatchProcessing;
using MailBatch.Console.BatchProcessing.Result;
using MailBatch.Console.Configuration;
using MailBatch.Console.DependencyInjection;
using MailBatch.Console.Logging;
using MailBatch.Console.Options;
using MailBatch.Console.ReceivedMails.State;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

int exitCode = BatchExitCodes.SUCCESS;
string runId = Guid.NewGuid().ToString();
using CancellationTokenSource cancellationTokenSource = new();

// 設定の読み込みや検証に失敗した場合も、少なくとも標準エラーへ原因を出力する。
Log.Logger = SerilogLoggerFactory.CreateBootstrap(runId);

// Ctrl + C を入力した場合の挙動を設定する
// DB接続等、外部接続が開いたままプログラムが終了することを防ぐため
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true; // デフォルトの処理（プログラムを即終了）をキャンセル
    cancellationTokenSource.Cancel(); // キャンセル要求を発行
};

try
{
    LoadedConfiguration loadedConfiguration = AppConfiguration.Load(args);
    AppOptions options = loadedConfiguration.Options;
    BatchOptions batchOptions = options.Batch;

    await Log.CloseAndFlushAsync();
    Log.Logger = SerilogLoggerFactory.Create(loadedConfiguration, runId);

    await using ServiceProvider serviceProvider = new ServiceCollection()
        .AddBatchApplication(options, runId, Log.Logger)
        .BuildServiceProvider();

    BatchRunner runner = serviceProvider.GetRequiredService<BatchRunner>();
    exitCode = await runner.RunAsync(cancellationTokenSource.Token);

    // 正常にバッチ処理を完了した場合のみ、保持期間を過ぎたデータを削除する。
    _ = new LogRetentionCleaner(batchOptions).TryDeleteExpiredLogs();
    _ = new SqliteRetentionCleaner(batchOptions).TryDeleteExpiredRecords();
}
catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
{
    exitCode = BatchExitCodes.CANCELED;

    Log.Warning(
        "Mail batch canceled. RunId={RunId}",
        runId);
}
catch (Exception ex)
{
    exitCode = BatchExitCodes.FATAL_ERROR;

    Log.Fatal(
        ex,
        "Mail batch failed with an unhandled exception. RunId={RunId}",
        runId);
}
finally
{
    // メモリ上に残っているログを書き出してロガーを終了する
    await Log.CloseAndFlushAsync();
}

return exitCode;
