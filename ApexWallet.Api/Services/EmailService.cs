using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ApexWallet.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            // 1. Extract settings safely from appsettings.json
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");

            // 2. Configure the SMTP client network parameters
            using var client = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true // Secure connection required by Google
            };

            // 3. Construct the mail message packet
            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail!),
                Subject = subject,
                Body = body,
                IsBodyHtml = true // Allows us to use clean HTML tags in our email formatting!
            };

            mailMessage.To.Add(toEmail);

            // 4. Dispatch the email across the network asynchronously
            await client.SendMailAsync(mailMessage);
        }
    }
}