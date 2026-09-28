using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SwivelWater.API.Models;

namespace SwivelWater.API.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody)
    {
        var email = new MimeMessage();

        email.From.Add(
            new MailboxAddress(
                _settings.FromName,
                _settings.FromEmail
            )
        );

        email.To.Add(
            MailboxAddress.Parse(recipientEmail)
        );

        email.Subject = subject;

        email.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var smtp = new SmtpClient();

        // Use a valid ASCII hostname for the SMTP EHLO command.
        smtp.LocalDomain = "localhost";

        await smtp.ConnectAsync(
            _settings.SmtpHost,
            _settings.SmtpPort,
            SecureSocketOptions.StartTls
        );

        await smtp.AuthenticateAsync(
            _settings.SmtpUsername,
            _settings.SmtpPassword
        );

        await smtp.SendAsync(email);

        await smtp.DisconnectAsync(true);
    }
}