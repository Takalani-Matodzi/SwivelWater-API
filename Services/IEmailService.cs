namespace SwivelWater.API.Services;

public interface IEmailService
{
    Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string htmlBody);
}