using MailKit.Net.Smtp;
using TestMailSender.Configuration;
using TestMailSender.Mail;

var exitCode = 0;

try
{
    var options = AppConfiguration.Load(args);
    var message = MailMessageFactory.Create(options);

    using var smtpClient = new SmtpClient();
    await smtpClient.ConnectAsync(options.Smtp.Host, options.Smtp.Port, SmtpSecurity.ToSecureSocketOptions(options.Smtp.UseSsl));

    if (!string.IsNullOrWhiteSpace(options.Smtp.UserName))
    {
        await smtpClient.AuthenticateAsync(options.Smtp.UserName, options.Smtp.Password!);
    }

    await smtpClient.SendAsync(message);
    await smtpClient.DisconnectAsync(true);

    Console.WriteLine("Test mail sent.");
    Console.WriteLine($"Mode: {options.Mail.Mode}");
    Console.WriteLine($"SMTP: {options.Smtp.Host}:{options.Smtp.Port}");
    Console.WriteLine($"From: {message.From}");
    Console.WriteLine($"To: {message.To}");
    Console.WriteLine($"Subject: {message.Subject}");
    Console.WriteLine($"Message-Id: {message.MessageId}");
}
catch (Exception ex)
{
    exitCode = 1;
    Console.Error.WriteLine($"Test mail send failed: {ex.Message}");
}

return exitCode;
