using Microsoft.Extensions.Configuration;
using TestMailSender.Options;

namespace TestMailSender.Configuration;

internal static class AppConfiguration
{
    /// <summary>
    /// アプリケーション設定ファイル、環境変数、コマンドライン引数から設定を読み込み、検証済みのオプションを返します。
    /// </summary>
    public static AppOptions Load(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "TESTMAILSENDER_")
            .AddCommandLine(args)
            .Build();

        AppOptions options = configuration.Get<AppOptions>() ?? new AppOptions();
        options.Validate();
        return options;
    }
}
