using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PerfumeShop.Api.Models;

namespace PerfumeShop.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public string Create(User u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = cfg["Jwt:Issuer"],
            Audience = cfg["Jwt:Audience"],
            Expires = DateTime.UtcNow.AddHours(8),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", u.Id.ToString()),
                new Claim("email", u.Email),
                new Claim("name", u.FullName),
                new Claim("role", u.Role)
            }),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

public static class UserExt
{
    // logged-in user ki Id token se nikalta hai
    public static int Id(this ClaimsPrincipal u) => int.Parse(u.FindFirstValue("sub")!);
}
