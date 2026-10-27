using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PerfumeShop.Api.Models;

namespace PerfumeShop.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public bool Configured =>
            _settings.User.Contains('@') &&
            !string.IsNullOrWhiteSpace(_settings.AppPassword);

        private string FromEmail =>
            string.IsNullOrWhiteSpace(_settings.FromEmail) ? _settings.User : _settings.FromEmail.Trim();

        private string FromName =>
            string.IsNullOrWhiteSpace(_settings.FromName) ? "Aurelle Perfumes" : _settings.FromName.Trim();

        private static IEnumerable<string> Split(string? list) =>
            (list ?? "").Split(new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            bool copyAdmin = false)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress(FromName, FromEmail));
            email.Subject = subject;

            if (!string.IsNullOrWhiteSpace(_settings.TestToEmail))
            {
                // test mode: asli customer ko kuch nahi jaata, sab kuch ek test inbox mein
                email.To.Add(MailboxAddress.Parse(_settings.TestToEmail.Trim()));
                email.Subject = $"[TEST for {toEmail}] {subject}";
            }
            else
            {
                email.To.Add(MailboxAddress.Parse(toEmail));

                if (copyAdmin)
                {
                    foreach (var cc in Split(_settings.CcEmail))
                        email.Cc.Add(MailboxAddress.Parse(cc));

                    foreach (var bcc in Split(_settings.BccEmail))
                        email.Bcc.Add(MailboxAddress.Parse(bcc));
                }
            }

            if (!string.IsNullOrWhiteSpace(_settings.ReplyTo))
                email.ReplyTo.Add(MailboxAddress.Parse(_settings.ReplyTo.Trim()));

            email.Body = new TextPart("html")
            {
                Text = body
            };

            using var smtp = new SmtpClient { Timeout = 20000 };

            await smtp.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls);

            await smtp.AuthenticateAsync(
                _settings.User,
                _settings.AppPassword.Replace(" ", ""));

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}
