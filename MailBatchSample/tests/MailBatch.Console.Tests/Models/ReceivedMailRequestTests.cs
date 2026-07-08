using System.Text.Json;
using MailBatch.Console.Models;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.Models;

public sealed class ReceivedMailRequestTests
{
    [Fact]
    public void Validate_ReturnsNoErrorsWhenSubjectAndBodyAreWithinLimits()
    {
        ReceivedMailRequest request = CreateRequest(
            subject: new string('s', ReceivedMailRequest.MaxSubjectLength),
            body: new string('b', ReceivedMailRequest.MaxBodyLength));

        IReadOnlyList<string> errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ReturnsErrorsWhenSubjectAndBodyExceedLimits()
    {
        ReceivedMailRequest request = CreateRequest(
            subject: new string('s', ReceivedMailRequest.MaxSubjectLength + 1),
            body: new string('b', ReceivedMailRequest.MaxBodyLength + 1));

        IReadOnlyList<string> errors = request.Validate();

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, error => error.Contains("Subject length", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Body length", StringComparison.Ordinal));
    }

    [Fact]
    public void Serialize_DoesNotIncludeInternalUid()
    {
        ReceivedMailRequest request = CreateRequest("subject", "body") with { Uid = new UniqueId(123) };

        string json = JsonSerializer.Serialize(request);

        Assert.DoesNotContain("Uid", json, StringComparison.Ordinal);
    }

    private static ReceivedMailRequest CreateRequest(string subject, string? body)
    {
        return new ReceivedMailRequest(
            MessageId: "<message@example.com>",
            Sender: "sender@example.com",
            Subject: subject,
            Body: body,
            ReceivedAt: DateTimeOffset.UnixEpoch);
    }
}
