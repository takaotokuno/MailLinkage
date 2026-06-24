using Xunit;
using MailBatch.Console.Mail;
using MimeKit;

namespace MailBatch.Console.Tests.Mail;

public sealed class ReceivedMailMapperTests
{
    [Fact]
    public void ToRequest_UsesInternalDateAndPlainTextBodyWhenAvailable()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.MessageId = "<message-1@example.com>";
        message.Subject = "Subject";
        message.Body = new TextPart("plain") { Text = "plain body" };
        var internalDate = new DateTimeOffset(2026, 6, 24, 12, 30, 0, TimeSpan.FromHours(9));

        var request = ReceivedMailMapper.ToRequest(message, internalDate);

        Assert.Equal("<message-1@example.com>", request.MessageId);
        Assert.Equal("sender@example.com", request.Sender);
        Assert.Equal("Subject", request.Subject);
        Assert.Equal("plain body", request.Body);
        Assert.Equal(internalDate.ToUniversalTime(), request.ReceivedAt);
    }

    [Fact]
    public void ToRequest_ConvertsHtmlBodyToReadableTextWhenPlainTextBodyIsMissing()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.MessageId = "<message-2@example.com>";
        message.Subject = "HTML";
        message.Body = new TextPart("html") { Text = "<p>Hello&nbsp;<strong>world</strong></p>" };

        var request = ReceivedMailMapper.ToRequest(message, DateTimeOffset.UnixEpoch);

        Assert.Equal("Hello  world", request.Body);
    }

    [Fact]
    public void ToRequest_UsesMessageDateWhenInternalDateIsMissing()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));
        message.MessageId = "<message-date@example.com>";
        message.Subject = "Date fallback";
        message.Body = new TextPart("plain") { Text = "body" };
        message.Date = new DateTimeOffset(2026, 6, 24, 10, 0, 0, TimeSpan.FromHours(9));

        var request = ReceivedMailMapper.ToRequest(message, internalDate: null);

        Assert.Equal(message.Date.ToUniversalTime(), request.ReceivedAt);
    }

    [Fact]
    public void ToRequest_GeneratesMessageIdAndAllowsNullBodyWhenSourceFieldsAreMissing()
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("sender@example.com"));

        var request = ReceivedMailMapper.ToRequest(message, DateTimeOffset.UnixEpoch);

        Assert.StartsWith("<missing-", request.MessageId);
        Assert.Equal("sender@example.com", request.Sender);
        Assert.Equal(string.Empty, request.Subject);
        Assert.Null(request.Body);
    }
}
