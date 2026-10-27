using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers;

// NAYA: Wishlist. Har endpoint logged-in user ki hi wishlist dekhta/badalta hai.
[ApiController, Authorize]
[Route("api/wishlist")]
public class WishlistController(AppDbContext db) : ControllerBase
{
    // wishlist ke products (product list jaisa hi shape, taaki frontend ProductCard reuse kare)
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var uid = User.Id();
        var items = await db.WishlistItems
            .Where(w => w.UserId == uid)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new
            {
                Id = w.Product!.Id,
                Name = w.Product!.Name,
                Price = w.Product!.Price,
                SizeMl = w.Product!.SizeMl,
                Gender = w.Product!.Gender,
                ImageUrl = w.Product!.ImageUrl,
                Brand = w.Product!.Brand!.Name,
                AvgRating = w.Product!.Reviews.Any() ? w.Product!.Reviews.Average(r => r.Rating) : 0
            }).ToListAsync();
        return Ok(items);
    }

    [HttpPost("{productId:int}")]
    public async Task<IActionResult> Add(int productId)
    {
        var uid = User.Id();
        if (!await db.Products.AnyAsync(p => p.Id == productId))
            return NotFound(new { message = "Product not found" });

        if (!await db.WishlistItems.AnyAsync(w => w.UserId == uid && w.ProductId == productId))
        {
            db.WishlistItems.Add(new WishlistItem { UserId = uid, ProductId = productId });
            await db.SaveChangesAsync();
        }
        return await List();
    }

    [HttpDelete("{productId:int}")]
    public async Task<IActionResult> Remove(int productId)
    {
        var uid = User.Id();
        await db.WishlistItems.Where(w => w.UserId == uid && w.ProductId == productId).ExecuteDeleteAsync();
        return await List();
    }
}
