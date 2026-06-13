using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

internal static class AppConfiguration
{
    public static AppOptions Load(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "MAILBATCH_")
            .AddCommandLine(args)
            .Build();

        var options = configuration.Get<AppOptions>() ?? new AppOptions();
        options.Validate();
        return options;
    }
}
