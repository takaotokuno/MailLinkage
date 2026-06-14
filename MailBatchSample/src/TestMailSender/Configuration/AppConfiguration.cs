using Microsoft.Extensions.Configuration;
using TestMailSender.Options;

namespace TestMailSender.Configuration;

internal static class AppConfiguration
{
    public static AppOptions Load(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "TESTMAILSENDER_")
            .AddCommandLine(args)
            .Build();

        var options = configuration.Get<AppOptions>() ?? new AppOptions();
        options.Validate();
        return options;
    }
}
