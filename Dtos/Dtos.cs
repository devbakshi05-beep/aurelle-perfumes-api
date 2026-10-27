using System.ComponentModel.DataAnnotations;

namespace PerfumeShop.Api.Dtos;

public class RegisterDto
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, MinLength(6)] public string Password { get; set; } = "";
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

public class CartAddDto
{
    public int ProductId { get; set; }
    [Range(1, 50)] public int Quantity { get; set; } = 1;
}

public class CartUpdateDto
{
    [Range(1, 50)] public int Quantity { get; set; }
}

public class CheckoutDto
{
    [Required, StringLength(300)] public string ShippingAddress { get; set; } = "";
    [RegularExpression("COD|Online")] public string PaymentMethod { get; set; } = "COD";
}

public class ReviewDto
{
    [Range(1, 5)] public int Rating { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = "";
}

public class StatusDto
{
    [Required] public string Status { get; set; } = "";
}

public class ProductSaveDto
{
    [Required, StringLength(150)] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [Range(1, 1000000)] public decimal Price { get; set; }
    [Range(1, 1000)] public int SizeMl { get; set; }
    [RegularExpression("Men|Women|Unisex")] public string Gender { get; set; } = "Unisex";
    public string TopNotes { get; set; } = "";
    public string HeartNotes { get; set; } = "";
    public string BaseNotes { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    [Range(0, 100000)] public int Stock { get; set; }
    public bool IsFeatured { get; set; }
    [Range(1, int.MaxValue)] public int BrandId { get; set; }
}

// NAYE: payment ke DTOs
public class VerifyPaymentDto
{
    public int OrderId { get; set; }
    [Required] public string RazorpayOrderId { get; set; } = "";
    [Required] public string RazorpayPaymentId { get; set; } = "";
    [Required] public string RazorpaySignature { get; set; } = "";
}

public class DemoPaymentDto
{
    public int OrderId { get; set; }
    public bool Success { get; set; }
}

// NAYE: email verification ke DTOs
public class VerifyEmailDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, RegularExpression(@"^\d{6}$")] public string Code { get; set; } = "";
}

public class ResendCodeDto
{
    [Required, EmailAddress] public string Email { get; set; } = "";
}
