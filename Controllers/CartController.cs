using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers;

[ApiController, Authorize]
[Route("api/cart")]
public class CartController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var uid = User.Id();
        var items = await db.CartItems.Where(c => c.UserId == uid)
            .Select(c => new
            {
                c.Id, c.ProductId,
                Name = c.Product!.Name,
                Price = c.Product!.Price,
                ImageUrl = c.Product!.ImageUrl,
                c.Quantity,
                LineTotal = c.Product!.Price * c.Quantity
            }).ToListAsync();
        return Ok(new { items, total = items.Sum(i => i.LineTotal) });
    }

    [HttpPost]
    public async Task<IActionResult> Add(CartAddDto dto)
    {
        var uid = User.Id();
        var p = await db.Products.FindAsync(dto.ProductId);
        if (p is null) return NotFound(new { message = "Product not found" });

        var item = await db.CartItems.FirstOrDefaultAsync(c => c.UserId == uid && c.ProductId == p.Id);
        var newQty = (item?.Quantity ?? 0) + dto.Quantity;
        if (newQty > p.Stock) return BadRequest(new { message = $"Only {p.Stock} in stock" });

        if (item is null) db.CartItems.Add(new CartItem { UserId = uid, ProductId = p.Id, Quantity = newQty });
        else item.Quantity = newQty;

        await db.SaveChangesAsync();
        return await Get();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CartUpdateDto dto)
    {
        var uid = User.Id();
        var item = await db.CartItems.Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == uid);
        if (item is null) return NotFound();
        if (dto.Quantity > item.Product!.Stock)
            return BadRequest(new { message = $"Only {item.Product.Stock} in stock" });

        item.Quantity = dto.Quantity;
        await db.SaveChangesAsync();
        return await Get();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        var uid = User.Id();
        await db.CartItems.Where(c => c.Id == id && c.UserId == uid).ExecuteDeleteAsync();
        return await Get();
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        var uid = User.Id();
        await db.CartItems.Where(c => c.UserId == uid).ExecuteDeleteAsync();
        return await Get();
    }
}
