using MailBatch.Console.Options;
using Microsoft.Extensions.Configuration;

namespace MailBatch.Console.Configuration;

/// <summary>
/// 読み込み済みの設定ルートと検証済みオプションを保持します。
/// </summary>
internal sealed record LoadedConfiguration(IConfigurationRoot Configuration, AppOptions Options);
