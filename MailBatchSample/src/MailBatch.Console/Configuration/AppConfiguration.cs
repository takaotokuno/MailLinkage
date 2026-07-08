using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

internal static class AppConfiguration
{
    /// <summary>
    /// コマンドライン引数、環境変数、設定ファイルからアプリケーション設定を読み込み、検証します。
    /// </summary>
    public static AppOptions Load(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "MAILBATCH_")
            .AddCommandLine(args)
            .Build();

        AppOptions options = configuration.Get<AppOptions>() ?? new AppOptions();
        options.Validate();
        return options;
    }
}
