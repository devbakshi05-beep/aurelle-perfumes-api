using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Models;

namespace PerfumeShop.Api.Controllers;

[ApiController, Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats() => Ok(new
    {
        orders = await db.Orders.CountAsync(),
        // revenue sirf unka jinka payment aa chuka hai (Paid)
        revenue = await db.Orders.Where(o => o.PaymentStatus == "Paid" && o.Status != "Cancelled")
                      .SumAsync(o => (decimal?)o.Total) ?? 0,
        products = await db.Products.CountAsync(),
        customers = await db.Users.CountAsync(u => u.Role == "Customer"),
        lowStock = await db.Products.CountAsync(p => p.Stock < 10)
    });

    [HttpPost("brands")]
    public async Task<IActionResult> AddBrand([FromBody] string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Name required" });
        var b = new Brand { Name = name.Trim() };
        db.Brands.Add(b);
        await db.SaveChangesAsync();
        return Ok(new { b.Id, b.Name });
    }

    [HttpPost("products")]
    public async Task<IActionResult> Create(ProductSaveDto dto)
    {
        if (!await db.Brands.AnyAsync(b => b.Id == dto.BrandId)) return BadRequest(new { message = "Invalid brand" });
        var p = new Product();
        Apply(p, dto);
        db.Products.Add(p);
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpPut("products/{id:int}")]
    public async Task<IActionResult> Update(int id, ProductSaveDto dto)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        if (!await db.Brands.AnyAsync(b => b.Id == dto.BrandId)) return BadRequest(new { message = "Invalid brand" });
        Apply(p, dto);
        await db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await db.Products.FindAsync(id);
        if (p is null) return NotFound();
        if (await db.OrderItems.AnyAsync(i => i.ProductId == id))
            return Conflict(new { message = "Product has past orders. Set Stock to 0 instead of deleting." });
        db.Products.Remove(p);
        await db.SaveChangesAsync();
        return NoContent();
    }

    static void Apply(Product p, ProductSaveDto d)
    {
        p.Name = d.Name.Trim(); p.Description = d.Description; p.Price = d.Price; p.SizeMl = d.SizeMl;
        p.Gender = d.Gender; p.TopNotes = d.TopNotes; p.HeartNotes = d.HeartNotes; p.BaseNotes = d.BaseNotes;
        p.ImageUrl = d.ImageUrl; p.Stock = d.Stock; p.IsFeatured = d.IsFeatured; p.BrandId = d.BrandId;
    }
}
