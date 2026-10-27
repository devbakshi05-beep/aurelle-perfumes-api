using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MimeKit;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers
{
    // Email settings check karne ke liye. Ye sirf Development mein chalta hai (production mein 404).
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly EmailSettings _settings;
        private readonly IWebHostEnvironment _env;

        public EmailController(
            IEmailService emailService,
            IOptions<EmailSettings> settings,
            IWebHostEnvironment env)
        {
            _emailService = emailService;
            _settings = settings.Value;
            _env = env;
        }

        // Simple test: apni hi email par ek mail bhejta hai
        [HttpGet("send-test")]
        public async Task<IActionResult> SendTestEmail()
        {
            if (!_env.IsDevelopment()) return NotFound();

            if (!_emailService.Configured)
                return BadRequest(new { Success = false, Message = "Email:User aur Email:AppPassword set nahi hain" });

            try
            {
                await _emailService.SendEmailAsync(
                    _settings.User,
                    "SMTP Test",
                    "<h2>Email Sent Successfully</h2><p>Aurelle Perfume Test Email</p>",
                    copyAdmin: true);

                return Ok(new { Success = true, Message = "Email Sent Successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { Success = false, Message = ex.Message });
            }
        }

        // Poora check ek saath: settings -> internet/port -> Gmail login -> (optional) asli mail.
        // Kholo: /api/email/diagnose            (login tak check)
        //        /api/email/diagnose?send=true  (asli test mail bhi bhejta hai)
        [HttpGet("diagnose")]
        public async Task<IActionResult> Diagnose([FromQuery] bool send = false)
        {
            if (!_env.IsDevelopment()) return NotFound();

            var steps = new List<object>();
            var allOk = true;

            void Add(string step, bool ok, string message, string? hint = null)
            {
                if (!ok) allOk = false;
                steps.Add(new { step, ok, message, hint });
            }

            // ---------- 1. Settings ----------
            var user = (_settings.User ?? "").Trim();
            var pwd = (_settings.AppPassword ?? "").Replace(" ", "");
            var problems = new List<string>();

            if (!user.Contains('@')) problems.Add("Email:User khali ya galat hai.");

            if (pwd.Length == 0)
                problems.Add("Email:AppPassword khali hai.");
            else if (pwd.Length != 16 || !pwd.All(char.IsLetter))
                problems.Add("AppPassword Google ke App Password jaisa nahi lag raha (wo sirf 16 letters ka hota hai, jaise abcdefghijklmnop). Placeholder ya normal Gmail password toh nahi daal diya?");

            if (problems.Count > 0)
            {
                Add("1. Settings", false, string.Join(" ", problems),
                    "appsettings.json ka Email block theek karo, phir backend Stop -> F5 (restart) karo.");
                return Ok(new { allOk, steps });
            }

            Add("1. Settings", true, "User aur AppPassword bhare hue hain.");

            var warnings = new List<string>();
            if (!string.Equals(_settings.Host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Host '{_settings.Host}' hai, Gmail ke liye smtp.gmail.com chahiye.");
            if (_settings.Port != 587)
                warnings.Add($"Port {_settings.Port} hai, Gmail ke liye 587 chahiye (StartTls).");
            if (!string.IsNullOrWhiteSpace(_settings.FromEmail) &&
                !string.Equals(_settings.FromEmail.Trim(), user, StringComparison.OrdinalIgnoreCase))
                warnings.Add("FromEmail, User se alag hai. Gmail use wapas User wale address mein badal dega.");
            if (!string.IsNullOrWhiteSpace(_settings.TestToEmail))
                warnings.Add($"TestToEmail set hai, isliye SAARI emails {_settings.TestToEmail} par jayengi (asli user ko nahi).");
            if (warnings.Count > 0)
                Add("1b. Warnings", true, string.Join(" | ", warnings));

            // ---------- 2. Internet / port ----------
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var connect = tcp.ConnectAsync(_settings.Host, _settings.Port);
                if (await Task.WhenAny(connect, Task.Delay(8000)) != connect)
                    throw new TimeoutException("8 second mein connect nahi hua.");
                await connect;
                Add("2. Internet / port", true, $"{_settings.Host}:{_settings.Port} tak pahunch gaye.");
            }
            catch (Exception ex)
            {
                Add("2. Internet / port", false, $"{ex.GetType().Name}: {ex.Message}",
                    "Internet, VPN, Firewall ya Antivirus check karo (port 587 block ho sakta hai). Mobile hotspot par try karo. Command Prompt mein: powershell Test-NetConnection smtp.gmail.com -Port 587");
                return Ok(new { allOk, steps });
            }

            // ---------- 3-5. Gmail connect, login, (optional) send ----------
            using var smtp = new SmtpClient { Timeout = 15000 };
            try
            {
                await smtp.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls);
                Add("3. SMTP connect", true, "Gmail server se secure connection ban gaya.");

                await smtp.AuthenticateAsync(user, pwd);
                Add("4. Login", true, "Gmail ne User + App Password accept kar liya.");

                if (send)
                {
                    var msg = new MimeMessage();
                    msg.From.Add(new MailboxAddress("Aurelle Diagnose", user));
                    msg.To.Add(MailboxAddress.Parse(user));
                    msg.Subject = "Aurelle diagnose test";
                    msg.Body = new TextPart("plain") { Text = "Ye test email hai. Mil gayi toh email setup sahi hai." };

                    await smtp.SendAsync(msg);
                    Add("5. Send", true,
                        $"Test email {user} par bhej di gayi. Inbox, Spam, Promotions aur All Mail folder dekho.");
                }
                else
                {
                    Add("5. Send", true, "Skip kiya. Asli test mail ke liye URL ke aakhir mein ?send=true lagao.");
                }
            }
            catch (AuthenticationException ex)
            {
                Add("4. Login", false, ex.Message,
                    "Gmail ne password reject kiya. Naya App Password banao (myaccount.google.com/apppasswords), 2-Step Verification ON hona chahiye, Email:User wahi Gmail ho jiske liye password bana. Password 16 letters, bina quotes ke.");
            }
            catch (SmtpCommandException ex)
            {
                Add("5. Send", false, $"{ex.StatusCode} ({(int)ex.StatusCode}): {ex.Message}",
                    "Gmail ne mail lene se mana kiya. Aam wajah: daily sending limit, recipient ka address galat, ya account par security block. Gmail inbox mein 'Security alert' mail dekho.");
            }
            catch (Exception ex)
            {
                // Gmail kabhi-kabhi galat password par connection tod deta hai, tab error AuthenticationException nahi
                // balki SmtpProtocolException banke aata hai. Message mein 5.7.8 / BadCredentials ho toh wajah password hai.
                var msg = ex.Message;
                var badCreds = msg.Contains("5.7.8") || msg.Contains("BadCredentials") ||
                               msg.Contains("Username and Password not accepted") || msg.Contains("535");

                if (badCreds)
                    Add("4. Login", false, $"{ex.GetType().Name}: {msg}",
                        "Gmail ne User + AppPassword reject kiya (BadCredentials). Naya App Password banao (myaccount.google.com/apppasswords) usi Gmail se jo Email:User mein hai, 2-Step Verification ON rakho. appsettings.Development.json ya User Secrets mein purani value toh nahi? Phir backend Stop -> F5.");
                else
                    Add("3. SMTP", false, $"{ex.GetType().Name}: {msg}",
                        "Connection beech mein toot gaya ya SSL/TLS dikkat. VPN/Antivirus band karke dekho, phir dobara try karo.");
            }
            finally
            {
                if (smtp.IsConnected) await smtp.DisconnectAsync(true);
            }

            return Ok(new { allOk, steps });
        }
    }
}
