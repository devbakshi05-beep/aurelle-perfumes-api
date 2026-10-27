namespace PerfumeShop.Api.Services
{
    public interface IEmailService
    {
        // true = Gmail User + AppPassword set hain, asli email jaayegi
        bool Configured { get; }

        // copyAdmin = true sirf order jaisi emails ke liye (CC/BCC lagta hai).
        // OTP wali email mein false rakho, warna admin ko verification code dikh jaayega.
        Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            bool copyAdmin = false);
    }
}
