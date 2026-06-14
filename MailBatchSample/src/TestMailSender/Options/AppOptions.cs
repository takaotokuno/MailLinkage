namespace TestMailSender.Options;

internal sealed class AppOptions
{
    public SmtpOptions Smtp { get; init; } = new();
    public MailOptions Mail { get; init; } = new();

    public void Validate()
    {
        Require(Smtp.Host, "Smtp:Host");
        if (Smtp.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Smtp:Port must be between 1 and 65535.");
        }

        Require(Mail.From, "Mail:From");
        Require(Mail.To, "Mail:To");
        Require(Mail.Body, "Mail:Body");
        Require(Mail.Mode, "Mail:Mode");
        Require(Mail.TargetSubject, "Mail:TargetSubject");
        Require(Mail.NonTargetSubject, "Mail:NonTargetSubject");

        var normalizedMode = Mail.Mode.Trim().ToLowerInvariant();
        if (normalizedMode is not ("target" or "nontarget" or "non-target" or "duplicate" or "custom"))
        {
            throw new InvalidOperationException("Mail:Mode must be target, nontarget, non-target, duplicate, or custom.");
        }

        if (normalizedMode == "duplicate")
        {
            Require(Mail.DuplicateMessageId, "Mail:DuplicateMessageId");
        }

        if (normalizedMode == "custom")
        {
            Require(Mail.Subject, "Mail:Subject");
        }
    }

    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }
}
