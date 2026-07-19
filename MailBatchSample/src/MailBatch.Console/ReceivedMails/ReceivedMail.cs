namespace MailBatch.Console.ReceivedMails;

/// <summary>
/// メールサーバーから取得した受信メールの内容を表します。
/// </summary>
internal sealed record ReceivedMail(
    ReceivedMailId MailId,
    string To,
    string Subject,
    string Body
)
{
    public const int MAX_SUBJECT_LENGTH = 8_192;
    public const int MAX_BODY_LENGTH = 10_000_000;

    /// <summary>
    /// 受信メール本文と件名が処理可能な制約を満たすか検証します。
    /// </summary>
    public void Validate()
    {
        List<string> errors = [];

        if (Subject.Length > MAX_SUBJECT_LENGTH)
        {
            errors.Add($"Subject length must be less than or equal to {MAX_SUBJECT_LENGTH} characters. Actual={Subject.Length}.");
        }

        if (Body?.Length > MAX_BODY_LENGTH)
        {
            errors.Add($"Body length must be less than or equal to {MAX_BODY_LENGTH} characters. Actual={Body.Length}.");
        }

        if (errors.Count > 0)
        {
            throw new ReceivedMailContentValidationException(errors);
        }
    }
}

/// <summary>
/// 受信メールの内容が処理可能な制約を満たさない場合に発生する例外です。
/// </summary>
internal sealed class ReceivedMailContentValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    /// <summary>
    /// 検証時に検出されたエラー一覧を取得します。
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// 受信メールそのものが壊れていて読み取り可能な形式ではない場合に発生する例外です。
/// </summary>
internal sealed class ReceivedMailFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
