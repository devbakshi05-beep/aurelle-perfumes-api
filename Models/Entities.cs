namespace PerfumeShop.Api.Models;

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Product> Products { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int SizeMl { get; set; }
    public string Gender { get; set; } = "Unisex";   // Men / Women / Unisex
    public string TopNotes { get; set; } = "";
    public string HeartNotes { get; set; } = "";
    public string BaseNotes { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int Stock { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BrandId { get; set; }
    public Brand? Brand { get; set; }
    public List<Review> Reviews { get; set; } = new();
}

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Customer";   // Customer / Admin

    // NAYE: email verification (OTP) ke fields
    public DateTime? EmailVerifiedAt { get; set; }        // null = email abhi verify nahi hua
    public string? VerificationCodeHash { get; set; }     // 6 digit code ka hash (asli code save nahi hota)
    public DateTime? VerificationExpiresAt { get; set; }  // code kab tak valid hai
    public int VerificationAttempts { get; set; }         // galat code ki koshishein
    public DateTime? VerificationSentAt { get; set; }     // code kab bheja (cooldown ke liye)
}

public class CartItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}

// NAYA: user ki wishlist (pasandida products)
public class WishlistItem
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public string Status { get; set; } = "Placed";           // Placed / Packed / Shipped / Delivered / Cancelled
    public string ShippingAddress { get; set; } = "";

    // NAYE payment fields
    public string PaymentMethod { get; set; } = "COD";       // COD / Online
    public string PaymentStatus { get; set; } = "Pending";   // Pending / Paid / Failed
    public string? PaymentRef { get; set; }                  // gateway ka payment id
    public string? GatewayOrderId { get; set; }              // Razorpay ka order id

    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }       // 1-5
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
