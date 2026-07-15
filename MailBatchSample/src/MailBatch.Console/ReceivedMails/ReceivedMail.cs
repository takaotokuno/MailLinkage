namespace MailBatch.Console.ReceivedMails;

internal sealed record ReceivedMail(
    ReceivedMailId MailId,
    string Sender,
    string Subject,
    string Body
)
{
    public const int MaxSubjectLength = 8_192;
    public const int MaxBodyLength = 10_000_000;

    public void Validate()
    {
        List<string> errors = [];

        if (Subject.Length > MaxSubjectLength)
        {
            errors.Add($"Subject length must be less than or equal to {MaxSubjectLength} characters. Actual={Subject.Length}.");
        }

        if (Body?.Length > MaxBodyLength)
        {
            errors.Add($"Body length must be less than or equal to {MaxBodyLength} characters. Actual={Body.Length}.");
        }

        if (errors.Count > 0)
        {
            throw new ReceivedMailContentValidationException(errors);
        }
    }
}

internal sealed class ReceivedMailContentValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
