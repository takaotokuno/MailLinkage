using System.Text.Json;
using MailBatch.Console.Api;
using MailBatch.Console.ReceivedMails;
using Xunit;

namespace MailBatch.Console.Tests.Api;

public sealed class ApiRequestTests
{
    [Fact]
    public void Validate_DoesNotThrowWhenSubjectAndBodyAreWithinLimits()
    {
        ApiRequest request = CreateRequest(
            subject: new string('s', ApiRequest.MaxSubjectLength),
            body: new string('b', ApiRequest.MaxBodyLength));

        Exception? exception = Record.Exception(request.Validate);

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ThrowsErrorMessagesWhenSubjectAndBodyExceedLimits()
    {
        ApiRequest request = CreateRequest(
            subject: new string('s', ApiRequest.MaxSubjectLength + 1),
            body: new string('b', ApiRequest.MaxBodyLength + 1));

        ApiRequestValidationException exception = Assert.Throws<ApiRequestValidationException>(request.Validate);

        Assert.Equal(2, exception.Errors.Count);
        Assert.Contains("Subject length", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Body length", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_DoesNotIncludeInternalMailId()
    {
        ReceivedMailRequest request = CreateRequest("subject", "body") with
        {
            MailId = new ReceivedMailId(123)
        };

        string json = JsonSerializer.Serialize(request);

        Assert.DoesNotContain("MailId", json, StringComparison.Ordinal);
    }

    private static ApiRequest CreateRequest(string subject, string? body)
    {
        return new ApiRequest(
            MessageId: "<message@example.com>",
            Sender: "sender@example.com",
            Subject: subject,
            Body: body,
            ReceivedAt: DateTimeOffset.UnixEpoch);
    }
}
