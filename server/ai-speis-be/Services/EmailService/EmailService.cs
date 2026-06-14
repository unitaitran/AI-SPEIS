using System.Net;
using System.Net.Mail;

namespace ai_speis_be.Services.EmailService
{
    public class EmailService : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpHost = GetRequiredSetting("Email:SmtpHost");
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var username = GetRequiredSetting("Email:Username");
            var password = GetRequiredSetting("Email:Password");
            var fromEmail = GetRequiredSetting("Email:FromEmail");
            var fromName = _configuration["Email:FromName"] ?? "AI-SPEIS";

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password)
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                To = {toEmail},
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }

        private string GetRequiredSetting(string key)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{key} is missing. Add it to .env or environment variables.");
            }

            return value;
        }
    }
}
