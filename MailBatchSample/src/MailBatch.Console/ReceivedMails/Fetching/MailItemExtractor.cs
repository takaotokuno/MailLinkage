using System.Text.RegularExpressions;

namespace MailBatch.Console.ReceivedMails.Fetching;

internal static partial class MailItemExtractor
{
    internal static ExtractedMailItem Extract(ReceivedMail mail)
    {
        ArgumentNullException.ThrowIfNull(mail);

        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(mail.Body))
        {
            errors.Add("Mail body must not be empty.");
            throw new MailExtractionException(errors);
        }

        MatchCollection matches = KeyLineRegex().Matches(mail.Body!);
        if (matches.Count == 0)
        {
            errors.Add("A key line in the format 'Key: alphanumeric-value' was not found.");
        }
        else if (matches.Count > 0)
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

    [GeneratedRegex(@"^Key:[ \t]*(?<key>[A-Za-z0-9]+)[ \t]*$", RegexOptions.Multiline)]
    private static partial Regex KeyLineRegex();
}

internal sealed class MailExtractionException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
