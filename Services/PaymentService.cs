using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PerfumeShop.Api.Models;

namespace PerfumeShop.Api.Services;

// Razorpay se baat karne wali service.
// appsettings mein Razorpay:KeyId aur Razorpay:KeySecret ho toh asli Razorpay TEST mode chalta hai,
// khali ho toh app "demo payment" mode mein chalti hai.
public class PaymentService(IConfiguration cfg, IHttpClientFactory httpFactory)
{
    public string KeyId => cfg["Razorpay:KeyId"] ?? "";
    private string KeySecret => cfg["Razorpay:KeySecret"] ?? "";

    public bool Configured => KeyId.StartsWith("rzp_") && KeySecret.Length > 0;

    // Razorpay amount paise mein leta hai (Rs 1 = 100 paise)
    public static long ToPaise(decimal rupees) =>
        (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);

    // Razorpay pe order banata hai aur uska id (order_xxx) wapas deta hai
    public async Task<string> CreateGatewayOrderAsync(Order order)
    {
        var client = httpFactory.CreateClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{KeyId}:{KeySecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var body = JsonSerializer.Serialize(new
        {
            amount = ToPaise(order.Total),
            currency = "INR",
            receipt = $"aurelle_{order.Id}"
        });

        var res = await client.PostAsync(
            "https://api.razorpay.com/v1/orders",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException("Razorpay order create failed: " + text);

        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    // signature = HMAC-SHA256(order_id + "|" + payment_id) key=KeySecret
    public bool VerifySignature(string gatewayOrderId, string paymentId, string signature)
    {
        var key = Encoding.UTF8.GetBytes(KeySecret);
        var data = Encoding.UTF8.GetBytes($"{gatewayOrderId}|{paymentId}");
        var expected = Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLowerInvariant();
        var given = signature.Trim().ToLowerInvariant();

        return expected.Length == given.Length &&
               CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }
}
