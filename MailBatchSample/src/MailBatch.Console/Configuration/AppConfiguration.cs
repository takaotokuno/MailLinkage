using Azure.Identity;
using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

internal static class AppConfiguration
{
    private const string DevelopmentEnvironmentName = "Development";
    private const string EnvironmentVariablePrefix = "MAILBATCH_";
    private const string AzureKeyVaultUriKey = "AzureKeyVault:VaultUri";

    /// <summary>
    /// コマンドライン引数、環境変数、設定ファイル、Azure Key Vaultからアプリケーション設定を読み込み、検証します。
    /// </summary>
    internal static LoadedConfiguration Load(string[] args)
    {
        string environmentName = GetEnvironmentName();
        bool isDevelopment = IsDevelopment(environmentName);

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: EnvironmentVariablePrefix)
            .AddCommandLine(args);

        if (!isDevelopment)
        {
            IConfigurationRoot bootstrapConfiguration = builder.Build();
            string? vaultUri = bootstrapConfiguration[AzureKeyVaultUriKey];
            if (string.IsNullOrWhiteSpace(vaultUri))
            {
                throw new InvalidOperationException(
                    $"{AzureKeyVaultUriKey} is required outside the Development environment.");
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

    private static string GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
    }

    private static bool IsDevelopment(string environmentName)
    {
        return string.Equals(
            environmentName,
            DevelopmentEnvironmentName,
            StringComparison.OrdinalIgnoreCase);
    }
}
