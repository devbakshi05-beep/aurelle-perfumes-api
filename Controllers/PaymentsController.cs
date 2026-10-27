using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers;

// NAYA: Payment gateway (Razorpay test mode ya demo mode)
[ApiController]
[Route("api/payments")]
public class PaymentsController(AppDbContext db, PaymentService pay) : ControllerBase
{
    // frontend ko batata hai ki "razorpay" mode chal raha hai ya "demo"
    [HttpGet("config")]
    public IActionResult Config() => Ok(new { mode = pay.Configured ? "razorpay" : "demo" });

    // Online order ke liye payment session banata hai
    [Authorize]
    [HttpPost("{orderId:int}/session")]
    public async Task<IActionResult> Session(int orderId)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == User.Id());
        if (order is null) return NotFound(new { message = "Order not found" });
        if (order.PaymentMethod != "Online") return BadRequest(new { message = "This order is Cash on Delivery" });
        if (order.PaymentStatus == "Paid") return BadRequest(new { message = "Order is already paid" });
        if (order.Status == "Cancelled") return BadRequest(new { message = "Order is cancelled" });

        var amount = PaymentService.ToPaise(order.Total);

        if (!pay.Configured)
            return Ok(new { mode = "demo", orderId = order.Id, amount, currency = "INR" });

        try
        {
            order.GatewayOrderId = await pay.CreateGatewayOrderAsync(order);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Payment gateway error: " + ex.Message });
        }

        return Ok(new
        {
            mode = "razorpay",
            orderId = order.Id,
            amount,
            currency = "INR",
            keyId = pay.KeyId,
            gatewayOrderId = order.GatewayOrderId
        });
    }

    // Razorpay se payment hone ke baad signature server pe verify hota hai (yehi asli security hai)
    [Authorize]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify(VerifyPaymentDto dto)
    {
        if (!pay.Configured) return BadRequest(new { message = "Razorpay is not configured" });

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == User.Id());
        if (order is null) return NotFound(new { message = "Order not found" });
        if (order.GatewayOrderId is null || order.GatewayOrderId != dto.RazorpayOrderId)
            return BadRequest(new { message = "Order mismatch" });

        if (!pay.VerifySignature(dto.RazorpayOrderId, dto.RazorpayPaymentId, dto.RazorpaySignature))
        {
            order.PaymentStatus = "Failed";
            await db.SaveChangesAsync();
            return BadRequest(new { message = "Payment verification failed" });
        }

        order.PaymentStatus = "Paid";
        order.PaymentRef = dto.RazorpayPaymentId;
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.PaymentStatus });
    }

    // DEMO mode: nakli payment (sirf tab chalta hai jab Razorpay keys set na hon)
    [Authorize]
    [HttpPost("demo-confirm")]
    public async Task<IActionResult> DemoConfirm(DemoPaymentDto dto)
    {
        if (pay.Configured)
            return BadRequest(new { message = "Demo payment is disabled when Razorpay keys are set" });

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == User.Id());
        if (order is null) return NotFound(new { message = "Order not found" });
        if (order.PaymentMethod != "Online") return BadRequest(new { message = "This order is Cash on Delivery" });
        if (order.PaymentStatus == "Paid") return BadRequest(new { message = "Order is already paid" });

        order.PaymentStatus = dto.Success ? "Paid" : "Failed";
        order.PaymentRef = dto.Success ? "DEMO-" + Guid.NewGuid().ToString("N")[..10].ToUpper() : null;
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.PaymentStatus });
    }
}
