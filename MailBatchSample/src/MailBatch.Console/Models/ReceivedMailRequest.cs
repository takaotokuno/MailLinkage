using System.Text.Json.Serialization;
using MailKit;

namespace MailBatch.Console.Models;

internal sealed record ReceivedMailRequest(
    string MessageId,
    string Sender,
    string Subject,
    string? Body,
    DateTimeOffset ReceivedAt)
{
    public const int MaxSubjectLength = 8_192;
    public const int MaxBodyLength = 10_000_000;

    // API に送るリクエスト本文には含めない、内部管理用の値
    [JsonIgnore]
    public UniqueId Uid { get; init; }

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
            throw new ReceivedMailRequestValidationException(errors);
        }
    }
}

internal sealed class ReceivedMailRequestValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
