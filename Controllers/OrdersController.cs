using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers;

[ApiController, Authorize]
[Route("api/orders")]
public class OrdersController(AppDbContext db, IEmailService mailer, ILogger<OrdersController> log) : ControllerBase
{
    static readonly string[] Statuses = { "Placed", "Packed", "Shipped", "Delivered", "Cancelled" };

    static object Shape(Order o) => new
    {
        o.Id, o.OrderDate, o.Total, o.Status, o.ShippingAddress,
        o.PaymentMethod, o.PaymentStatus, o.PaymentRef,
        Items = o.Items.Select(i => new { i.ProductId, Name = i.Product!.Name, i.Quantity, i.UnitPrice })
    };

    IQueryable<Order> Full() => db.Orders.Include(o => o.Items).ThenInclude(i => i.Product);

    // Order banata hai. COD ho ya Online, order pehle ban jaata hai.
    // Online ke liye frontend uske baad /api/payments/{id}/session se payment shuru karta hai.
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutDto dto)
    {
        var uid = User.Id();
        var cart = await db.CartItems.Include(c => c.Product).Where(c => c.UserId == uid).ToListAsync();
        if (cart.Count == 0) return BadRequest(new { message = "Cart is empty" });

        foreach (var c in cart)
            if (c.Quantity > c.Product!.Stock)
                return BadRequest(new { message = $"{c.Product.Name}: only {c.Product.Stock} left" });

        var order = new Order
        {
            UserId = uid,
            ShippingAddress = dto.ShippingAddress,
            PaymentMethod = dto.PaymentMethod,
            PaymentStatus = "Pending",
            Items = cart.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                Quantity = c.Quantity,
                UnitPrice = c.Product!.Price   // order ke time ki price save hoti hai
            }).ToList()
        };
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        foreach (var c in cart) c.Product!.Stock -= c.Quantity;
        db.Orders.Add(order);
        db.CartItems.RemoveRange(cart);
        await db.SaveChangesAsync();   // sab kuch ek saath save hota hai (atomic)

        await SendOrderEmailAsync(order.Id, uid);   // email fail ho toh bhi order safe rehta hai

        return Ok(new { order.Id, order.Total, order.Status, order.PaymentMethod, order.PaymentStatus });
    }

    // Order confirmation email: To = customer, CC/BCC = appsettings ke Email:CcEmail / BccEmail (shop owner)
    private async Task SendOrderEmailAsync(int orderId, int uid)
    {
        try
        {
            var customer = await db.Users.FindAsync(uid);
            var order = await Full().FirstOrDefaultAsync(o => o.Id == orderId);
            if (customer is null || order is null) return;

            var rows = string.Concat(order.Items.Select(i =>
                $"<tr><td style=\"padding:6px 12px\">{WebUtility.HtmlEncode(i.Product!.Name)}</td>" +
                $"<td style=\"padding:6px 12px\">{i.Quantity}</td>" +
                $"<td style=\"padding:6px 12px\">Rs. {i.UnitPrice:N0}</td></tr>"));

            var payment = order.PaymentMethod == "COD"
                ? "Cash on Delivery"
                : "Online payment (abhi " + order.PaymentStatus + ")";

            var html =
                $"<p>Hi {WebUtility.HtmlEncode(customer.FullName)},</p>" +
                $"<p>Aapka order <b>#{order.Id}</b> mil gaya hai. Shukriya!</p>" +
                "<table style=\"border-collapse:collapse\" border=\"1\">" +
                "<tr><th style=\"padding:6px 12px\">Product</th><th style=\"padding:6px 12px\">Qty</th><th style=\"padding:6px 12px\">Price</th></tr>" +
                rows + "</table>" +
                $"<p><b>Total: Rs. {order.Total:N0}</b></p>" +
                $"<p>Payment: {payment}<br/>Delivery address: {WebUtility.HtmlEncode(order.ShippingAddress)}</p>" +
                "<p>Aurelle Perfumes</p>";

            if (mailer.Configured)
                await mailer.SendEmailAsync(customer.Email, $"Aurelle order #{order.Id} confirmed", html, copyAdmin: true);
            else
                log.LogWarning("DEMO MODE (Gmail not configured). Order email would go to {Email} for order #{Id}", customer.Email, order.Id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Order confirmation email failed for order {Id}", orderId);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var uid = User.Id();
        var list = await Full().Where(o => o.UserId == uid).OrderByDescending(o => o.OrderDate).ToListAsync();
        return Ok(list.Select(Shape));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Track(int id)
    {
        var o = await Full().FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return NotFound();
        if (o.UserId != User.Id() && !User.IsInRole("Admin")) return Forbid();
        return Ok(Shape(o));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<IActionResult> All()
    {
        var orders = await Full().OrderByDescending(o => o.OrderDate).ToListAsync();
        var names = await db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Ok(orders.Select(o => new { Order = Shape(o), Customer = names.GetValueOrDefault(o.UserId) }));
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, StatusDto dto)
    {
        if (!Statuses.Contains(dto.Status))
            return BadRequest(new { message = "Status must be: " + string.Join(", ", Statuses) });
        var o = await db.Orders.FindAsync(id);
        if (o is null) return NotFound();

        o.Status = dto.Status;
        // COD order deliver hote hi paisa mil jaata hai
        if (dto.Status == "Delivered" && o.PaymentMethod == "COD") o.PaymentStatus = "Paid";

        await db.SaveChangesAsync();
        return Ok(new { o.Id, o.Status, o.PaymentStatus });
    }
}
