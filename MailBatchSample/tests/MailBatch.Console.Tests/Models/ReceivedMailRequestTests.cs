using System.Text.Json;
using MailBatch.Console.Models;
using MailKit;
using Xunit;

namespace MailBatch.Console.Tests.Models;

public sealed class ReceivedMailRequestTests
{
    [Fact]
    public void Validate_DoesNotThrowWhenSubjectAndBodyAreWithinLimits()
    {
        ReceivedMailRequest request = CreateRequest(
            subject: new string('s', ReceivedMailRequest.MaxSubjectLength),
            body: new string('b', ReceivedMailRequest.MaxBodyLength));

        Exception? exception = Record.Exception(request.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ThrowsErrorMessagesWhenSubjectAndBodyExceedLimits()
    {
        ReceivedMailRequest request = CreateRequest(
            subject: new string('s', ReceivedMailRequest.MaxSubjectLength + 1),
            body: new string('b', ReceivedMailRequest.MaxBodyLength + 1));

        ReceivedMailRequestValidationException exception = Assert.Throws<ReceivedMailRequestValidationException>(request.Validate);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Subject length", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Body length", exception.Message, StringComparison.Ordinal);
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
