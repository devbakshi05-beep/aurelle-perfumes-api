using System.Net.Http.Json;
using Microsoft.Extensions.Options;

// Ye file Gmail SMTP ki jagah Brevo ka HTTP API use karti hai.
// HTTPS (port 443) par chalta hai, isliye Render free tier par block nahi hota.
// Namespace apne project ke hisaab se badal lena (ya file ko existing namespace me rakh do).

public class BrevoOptions
{
    public string ApiKey { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "Aurelle Perfume";
    public string? ReplyTo { get; set; }
    public string? BccEmail { get; set; }

    // SIRF testing ke liye. Agar ye bhara hai to SAARE emails isi address par jaayenge.
    // Production (Render) me isse KHAALI rakho, warna asli users ko email nahi milegi.
    public string? TestToEmail { get; set; }
}

public class BrevoEmailService
{
    private readonly HttpClient _http;
    private readonly BrevoOptions _opt;
    private readonly ILogger<BrevoEmailService> _logger;

    public BrevoEmailService(HttpClient http, IOptions<BrevoOptions> opt, ILogger<BrevoEmailService> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    // Email bhejta hai. true = Brevo ne accept kar liya, false = fail hua (log me reason milega)
    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_opt.ApiKey) || string.IsNullOrWhiteSpace(_opt.SenderEmail))
        {
            _logger.LogError("Brevo ApiKey ya SenderEmail configure nahi hai (Render environment variables check karo).");
            return false;
        }

        var recipientEmail = string.IsNullOrWhiteSpace(_opt.TestToEmail) ? toEmail : _opt.TestToEmail;

        var payload = new Dictionary<string, object>
        {
            ["sender"] = new { name = _opt.SenderName, email = _opt.SenderEmail },
            ["to"] = new[] { new { email = recipientEmail, name = toName } },
            ["subject"] = subject,
            ["htmlContent"] = htmlBody
        };

        if (!string.IsNullOrWhiteSpace(_opt.ReplyTo))
            payload["replyTo"] = new { email = _opt.ReplyTo };

        if (!string.IsNullOrWhiteSpace(_opt.BccEmail))
            payload["bcc"] = new[] { new { email = _opt.BccEmail } };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email");
        request.Headers.Add("api-key", _opt.ApiKey);
        request.Headers.Add("accept", "application/json");
        request.Content = JsonContent.Create(payload);

        try
        {
            using var response = await _http.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return true;

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("Brevo email fail hua. Status: {Status}, Response: {Body}", (int)response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Brevo API call me exception aaya.");
            return false;
        }
    }
}

/* ---------------- Program.cs me ye 2 cheezein add karo ----------------

builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));

builder.Services.AddHttpClient<BrevoEmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.brevo.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

Phir jahan pehle Gmail SMTP wali email service inject hoti thi, wahan BrevoEmailService inject karo
(ya apne existing IEmailService interface ko implement karwa do, taaki baaki code na badalna pade).

---------------- Render Environment Variables (asli values yahan, GitHub me nahi) ----------------

Brevo__ApiKey        = (Brevo ki API key)
Brevo__SenderEmail   = (Brevo me verified sender email)
Brevo__SenderName    = Aurelle Perfume

(Double underscore __ ka matlab hai appsettings ke "Brevo" section ke andar wala field.)
*/