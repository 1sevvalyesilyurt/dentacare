using System.Net;
using System.Net.Mail;

namespace DentalClinic.Web.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var smtp = _config.GetSection("SmtpSettings");
            var host     = smtp["Host"];
            var port     = int.Parse(smtp["Port"] ?? "587");
            var username = smtp["Username"];
            var password = smtp["Password"];
            var fromAddress = smtp["FromAddress"] ?? username;
            var fromName    = smtp["FromName"] ?? "DentaCare";

            // If SMTP is not configured, log and skip silently (dev environment)
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
            {
                _logger.LogWarning("SMTP not configured. Skipping email to {To}: {Subject}", to, subject);
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl   = true,
                Timeout     = 10_000
            };

            using var message = new MailMessage
            {
                From       = new MailAddress(fromAddress!, fromName),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
            }
        }
    }
}
