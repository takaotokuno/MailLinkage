using Azure.Identity;
using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

/// <summary>
/// 設定ファイルと環境変数からアプリケーション設定を読み込みます。
/// </summary>
internal static class AppConfiguration
{
    private const string EnvironmentVariablePrefix = "MAILBATCH_";
    private const string AzureKeyVaultEnabledKey = "AzureKeyVault:Enabled";
    private const string AzureKeyVaultUriKey = "AzureKeyVault:VaultUri";

    /// <summary>
    /// コマンドライン引数、環境変数、設定ファイル、任意のAzure Key Vaultからアプリケーション設定を読み込み、検証します。
    /// </summary>
    internal static LoadedConfiguration Load(string[] args)
    {
        IConfigurationBuilder builder = CreateBaseBuilder(args);
        IConfigurationRoot bootstrapConfiguration = builder.Build();

        if (IsAzureKeyVaultEnabled(bootstrapConfiguration))
        {
            string? vaultUri = bootstrapConfiguration[AzureKeyVaultUriKey];
            if (string.IsNullOrWhiteSpace(vaultUri))
            {
                throw new InvalidOperationException(
                    $"{AzureKeyVaultUriKey} is required when {AzureKeyVaultEnabledKey} is true.");
            }

            _ = builder.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential())
                .AddEnvironmentVariables(prefix: EnvironmentVariablePrefix)
                .AddCommandLine(args);
        }

        IConfigurationRoot configuration = builder.Build();
        AppOptions options = configuration.Get<AppOptions>() ?? new AppOptions();
        options.Validate();
        return new LoadedConfiguration(configuration, options);
    }

    private static IConfigurationBuilder CreateBaseBuilder(string[] args)
    {
        string environmentName = GetEnvironmentName();

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: EnvironmentVariablePrefix)
            .AddCommandLine(args);
    }

    private static bool IsAzureKeyVaultEnabled(IConfiguration configuration) => configuration.GetValue<bool>(AzureKeyVaultEnabledKey);

    private static string GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
    }
}
