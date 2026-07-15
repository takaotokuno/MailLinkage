using System.Text.RegularExpressions;

namespace MailBatch.Console.ReceivedMails.Fetching;

/// <summary>
/// 受信メール本文から連携に必要な項目を抽出します。
/// </summary>
internal static partial class MailItemExtractor
{
    /// <summary>
    /// 受信メールからキー情報を抽出し、抽出結果を返します。
    /// </summary>
    internal static ExtractedMailItem Extract(ReceivedMail mail)
    {
        ArgumentNullException.ThrowIfNull(mail);

        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(mail.Body))
        {
            errors.Add("Mail body must not be empty.");
            throw new MailExtractionException(errors);
        }

        MatchCollection matches = KeyLineRegex().Matches(mail.Body);
        if (matches.Count == 0)
        {
            errors.Add("A key line in the format 'Key: alphanumeric-value' was not found.");
        }
        else if (matches.Count > 1)
        {
            errors.Add($"Multiple key lines were found. Expected exactly one, but found {matches.Count}.");
        }

        if (errors.Count > 0)
        {
            throw new MailExtractionException(errors);
        }

        string key = matches[0].Groups["key"].Value;

        return new ExtractedMailItem(mail.MailId, key);
    }

    /// <summary>
    /// Key行を抽出する正規表現を返します。
    /// </summary>
    [GeneratedRegex(@"^Key:[ \t]*(?<key>[A-Za-z0-9]+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex KeyLineRegex();
}

/// <summary>
/// メール本文から連携項目を抽出できない場合に発生する例外です。
/// </summary>
internal sealed class MailExtractionException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    /// <summary>
    /// 抽出時に検出されたエラー一覧を取得します。
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
