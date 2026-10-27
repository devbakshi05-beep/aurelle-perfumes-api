using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Services;
using AppUser = PerfumeShop.Api.Models.User;

namespace PerfumeShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, IEmailService mailer, IWebHostEnvironment env, ILogger<AuthController> log) : ControllerBase
{
    const int CodeMinutes = 10;      // code kitni der valid
    const int CooldownSeconds = 30;  // dobara code mangne se pehle wait
    const int MaxAttempts = 5;       // galat code ki limit

    // ---------- REGISTER: account banata hai (unverified) aur email pe 6 digit code bhejta hai ----------
    [HttpPost("register")]
    [EnableRateLimiting("mail")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        var mail = dto.Email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == mail);

        if (user is not null && user.EmailVerifiedAt is not null)
            return Conflict(new { message = "Email already registered" });

        if (user is null)
        {
            user = new AppUser { Email = mail };
            db.Users.Add(user);
        }
        // naya ya adhoora (unverified) account: naam aur password update
        user.FullName = dto.FullName.Trim();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        try
        {
            var (sent, devCode) = await SendCodeAsync(user);
            if (!sent)
                return StatusCode(429, new { message = $"Code abhi bheja gaya hai. {CooldownSeconds} second baad dobara try karo." });

            await db.SaveChangesAsync();
            return Ok(new { requiresVerification = true, email = user.Email, devCode });
        }
        catch (Exception ex)
        {
            return EmailFailed(ex);
        }
    }

    // ---------- VERIFY: sahi code dene par hi account active hota hai aur login ho jaata hai ----------
    [HttpPost("verify-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto dto)
    {
        var mail = dto.Email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == mail);

        if (user is null) return BadRequest(new { message = "Invalid code" });
        if (user.EmailVerifiedAt is not null)
            return BadRequest(new { message = "Email pehle se verified hai. Login karo." });
        if (user.VerificationCodeHash is null || user.VerificationExpiresAt is null || user.VerificationExpiresAt < DateTime.UtcNow)
            return BadRequest(new { message = "Code expire ho gaya. Naya code mangao." });
        if (user.VerificationAttempts >= MaxAttempts)
            return BadRequest(new { message = "Bahut zyada galat koshishein. Naya code mangao." });

        if (!BCrypt.Net.BCrypt.Verify(dto.Code.Trim(), user.VerificationCodeHash))
        {
            user.VerificationAttempts++;
            await db.SaveChangesAsync();
            return BadRequest(new { message = "Galat code" });
        }

        user.EmailVerifiedAt = DateTime.UtcNow;
        user.VerificationCodeHash = null;
        user.VerificationExpiresAt = null;
        user.VerificationAttempts = 0;
        await db.SaveChangesAsync();
        return Ok(AuthResult(user));
    }

    // ---------- RESEND: naya code ----------
    [HttpPost("resend-code")]
    [EnableRateLimiting("mail")]
    public async Task<IActionResult> ResendCode(ResendCodeDto dto)
    {
        var mail = dto.Email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == mail);

        // account hai ya nahi, ye bahar se pata nahi chalna chahiye
        if (user is null || user.EmailVerifiedAt is not null)
            return Ok(new { message = "Agar account mojood hai toh code bhej diya gaya hai." });

        try
        {
            var (sent, devCode) = await SendCodeAsync(user);
            if (!sent)
                return StatusCode(429, new { message = $"{CooldownSeconds} second baad dobara try karo." });

            await db.SaveChangesAsync();
            return Ok(new { message = "Naya code bhej diya gaya hai.", devCode });
        }
        catch (Exception ex)
        {
            return EmailFailed(ex);
        }
    }

    // ---------- LOGIN: password sahi ho par email verify na ho toh naya code bhej ke 403 deta hai ----------
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var mail = dto.Email.Trim().ToLower();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == mail);
        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password" });

        if (user.EmailVerifiedAt is null)
        {
            try
            {
                var (_, devCode) = await SendCodeAsync(user);
                await db.SaveChangesAsync();
                return StatusCode(403, new
                {
                    code = "EMAIL_NOT_VERIFIED",
                    message = "Pehle email verify karo. Code email pe bheja gaya hai.",
                    email = user.Email,
                    devCode
                });
            }
            catch (Exception ex)
            {
                return EmailFailed(ex);
            }
        }

        return Ok(AuthResult(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var u = await db.Users.FindAsync(User.Id());
        return u is null ? NotFound() : Ok(new { u.Id, u.FullName, u.Email, u.Role });
    }

    // 6 digit code banata hai, uska HASH user pe save karta hai, email bhejta hai.
    // Gmail set nahi hai toh code console mein print hota hai aur devCode wapas deta hai (sirf demo ke liye).
    // Cooldown chal raha ho toh kuch nahi karta (sent = false).
    private async Task<(bool sent, string? devCode)> SendCodeAsync(AppUser user)
    {
        if (user.VerificationSentAt is DateTime last && DateTime.UtcNow - last < TimeSpan.FromSeconds(CooldownSeconds))
            return (false, null);

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        user.VerificationCodeHash = BCrypt.Net.BCrypt.HashPassword(code);
        user.VerificationExpiresAt = DateTime.UtcNow.AddMinutes(CodeMinutes);
        user.VerificationAttempts = 0;
        user.VerificationSentAt = DateTime.UtcNow;

        if (mailer.Configured)
        {
            var html = $"<p>Hi {WebUtility.HtmlEncode(user.FullName)},</p>" +
                       $"<p>Aapka Aurelle verification code: <b style=\"font-size:24px;letter-spacing:4px\">{code}</b></p>" +
                       $"<p>Ye code {CodeMinutes} minute tak valid hai. Agar aapne register nahi kiya, toh is email ko ignore karein.</p>";
            await mailer.SendEmailAsync(user.Email, "Your Aurelle verification code", html);
            return (true, null);
        }

        log.LogWarning("DEMO MODE (Gmail not configured). Verification code for {Email}: {Code}", user.Email, code);
        return (true, code);
    }

    // email na ja paye toh: Development mein asli wajah dikhao, production mein sirf general message
    private IActionResult EmailFailed(Exception ex)
    {
        log.LogError(ex, "Verification email failed");
        var message = env.IsDevelopment()
            ? "Email bhej nahi paye: " + ex.Message
            : "Email bhej nahi paye. Gmail settings check karo.";
        return StatusCode(502, new { message });
    }

    private object AuthResult(AppUser u) => new
    {
        token = tokens.Create(u),
        user = new { u.Id, u.FullName, u.Email, u.Role }
    };
}
