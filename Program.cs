using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Render jaise hosts PORT environment variable dete hain; us port par 0.0.0.0 pe sunna zaroori hai ----
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// ---- Production safety: JWT key placeholder ya chhoti nahi honi chahiye (warna koi bhi fake token bana sakta hai) ----
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (!builder.Environment.IsDevelopment() && (jwtKey.Length < 32 || jwtKey.StartsWith("ChangeThis")))
    throw new InvalidOperationException(
        "Production mein Jwt__Key environment variable (32+ random characters) set karna zaroori hai.");

// ---- Database (Azure SQL serverless so jaata hai, isliye connection fail hone par kuch baar dobara try) ----
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 6,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddScoped<TokenService>();
builder.Services.AddHttpClient();                 // Razorpay API call ke liye
builder.Services.AddScoped<PaymentService>();

// Email (MailKit + Gmail SMTP): appsettings.json ke "Email" block se settings aati hain
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Sirf token paste karo (Bearer likhne ki zaroorat nahi)"
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

// ---- CORS: localhost + Cors__Origins (comma se alag, jaise https://aurelle.netlify.app) ----
var origins = (builder.Configuration["Cors:Origins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Concat(new[] { "http://localhost:3000", "http://localhost:4200" })
    .Select(o => o.TrimEnd('/'))
    .Distinct()
    .ToArray();

builder.Services.AddCors(o => o.AddPolicy("web", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---- Rate limit: koi bar-bar register/login karke tumhare Gmail se spam na karwa sake ----
// Render proxy ke peeche asli IP "X-Forwarded-For" header ke aakhri hisse mein hota hai
static string ClientIp(HttpContext ctx)
{
    var fwd = ctx.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(fwd))
        return fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
    return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

var isDev = builder.Environment.IsDevelopment();   // local testing mein limit khule rakho
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // email bhejne wale endpoints (register, resend-code): 6 baar / 10 minute / IP
    o.AddPolicy("mail", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "mail:" + ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isDev ? 1000 : 6,
            Window = TimeSpan.FromMinutes(10)
        }));

    // login / verify-email: 30 baar / 10 minute / IP
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        "auth:" + ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isDev ? 1000 : 30,
            Window = TimeSpan.FromMinutes(10)
        }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;   // "sub" aur "role" jaise ke taise rahenge
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Startup pe migrations apply karo aur admin user banao
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // purane users (jinke paas koi verification code nahi hai) ko verified maan lo, taaki wo login kar sakein
    db.Users.Where(u => u.EmailVerifiedAt == null && u.VerificationCodeHash == null)
        .ExecuteUpdate(s => s.SetProperty(u => u.EmailVerifiedAt, (DateTime?)DateTime.UtcNow));

    if (!db.Users.Any(u => u.Role == "Admin"))
    {
        var adminEmail = (app.Configuration["Admin:Email"] ?? "admin@aurelle.com").Trim().ToLower();
        var adminPassword = app.Configuration["Admin:Password"];

        // sirf local development mein default password; production mein Admin__Password dena zaroori hai
        if (string.IsNullOrWhiteSpace(adminPassword) && app.Environment.IsDevelopment())
            adminPassword = "Admin@123";

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            app.Logger.LogWarning("Admin user nahi bana. Admin__Password environment variable set karo.");
        }
        else
        {
            db.Users.Add(new User
            {
                FullName = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                Role = "Admin",
                EmailVerifiedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Render ka health check + "server jagao" URL. Database ko touch nahi karta (free DB ke vCore bachte hain)
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
