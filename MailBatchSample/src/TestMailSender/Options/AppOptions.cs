namespace TestMailSender.Options;

internal sealed class AppOptions
{
    public SmtpOptions Smtp { get; init; } = new();
    public MailOptions Mail { get; init; } = new();

    /// <summary>
    /// SMTP 設定とメール設定が送信に必要な条件を満たしているか検証します。
    /// </summary>
    public void Validate()
    {
        Require(Smtp.Host, "Smtp:Host");
        if (Smtp.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("Smtp:Port must be between 1 and 65535.");
        }
        
        if (!string.IsNullOrWhiteSpace(Smtp.UserName))
        {
            Require(Smtp.Password, "Smtp:Password");
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

    /// <summary>
    /// 指定された設定値が空でないことを確認し、未設定の場合は例外をスローします。
    /// </summary>
    private static void Require(string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }
    }
}
