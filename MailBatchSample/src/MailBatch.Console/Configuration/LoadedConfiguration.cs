using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

internal sealed record LoadedConfiguration(IConfigurationRoot Configuration, AppOptions Options);
