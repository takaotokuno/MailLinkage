using TestMailSender.Configuration;
using TestMailSender.Infrastructure;
using TestMailSender.Mail;
using TestMailSender.Services;

const int SUCCESS_EXIT_CODE = 0;
const int FATAL_ERROR_EXIT_CODE = 1;

int exitCode = SUCCESS_EXIT_CODE;

try
{
    TestMailSender.Options.AppOptions options = AppConfiguration.Load(args);
    MimeKit.MimeMessage message = MailMessageFactory.Create(options);
    ITestMailSender mailSender = new SmtpTestMailSender();

    await mailSender.SendAsync(options.Smtp, message);

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
    exitCode = FATAL_ERROR_EXIT_CODE;
    Console.Error.WriteLine($"Test mail send failed: {ex.Message}");
}

return exitCode;
