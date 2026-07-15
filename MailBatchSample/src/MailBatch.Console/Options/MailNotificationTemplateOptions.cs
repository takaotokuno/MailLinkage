namespace MailBatch.Console.Options;

internal sealed class MailNotificationTemplateOptions
{
    public string Name { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;

    public void Validate(string path)
    {
        OptionValidation.Require(Name, $"{path}:Name");
        OptionValidation.Require(Subject, $"{path}:Subject");
        OptionValidation.Require(Body, $"{path}:Body");
    }
}
