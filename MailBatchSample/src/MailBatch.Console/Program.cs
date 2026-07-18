using MailBatch.Console.BatchProcessing;
using MailBatch.Console.Configuration;
using MailBatch.Console.DependencyInjection;
using MailBatch.Console.Logging;
using MailBatch.Console.Options;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

int exitCode = 0;
BatchOptions? batchOptions = null;
string runId = Guid.NewGuid().ToString();
using CancellationTokenSource cancellationTokenSource = new();

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
    batchOptions = options.Batch;

    Log.Logger = SerilogLoggerFactory.Create(loadedConfiguration, runId);

    await using ServiceProvider serviceProvider = new ServiceCollection()
        .AddBatchApplication(options, runId, Log.Logger)
        .BuildServiceProvider();

    BatchRunner runner = serviceProvider.GetRequiredService<BatchRunner>();
    exitCode = await runner.RunAsync(cancellationTokenSource.Token);
}
catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
{
    exitCode = 130;

    Log.Warning(
        "Mail batch canceled. RunId={RunId}",
        runId);
}
catch (Exception ex)
{
    exitCode = 1;

    Log.Fatal(
        ex,
        "Mail batch failed with an unhandled exception. RunId={RunId}",
        runId);
}
finally
{
    // メモリ上に残っているログを書き出してロガーを終了する
    await Log.CloseAndFlushAsync();

    // 古いログを削除する
    if (batchOptions is not null)
    {
        new LogRetentionCleaner(batchOptions).DeleteExpiredLogs();
    }
}

return exitCode;
