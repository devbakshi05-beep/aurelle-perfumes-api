using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;
using PerfumeShop.Api.Dtos;
using PerfumeShop.Api.Models;
using PerfumeShop.Api.Services;

namespace PerfumeShop.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/reviews")]
public class ReviewsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int productId)
    {
        var list = await db.Reviews.Where(r => r.ProductId == productId)
            .Join(db.Users, r => r.UserId, u => u.Id,
                (r, u) => new { r.Id, r.Rating, r.Comment, r.CreatedAt, Reviewer = u.FullName })
            .OrderByDescending(r => r.CreatedAt).ToListAsync();

        return Ok(new
        {
            average = list.Count == 0 ? 0 : Math.Round(list.Average(r => r.Rating), 1),
            count = list.Count,
            reviews = list
        });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Add(int productId, ReviewDto dto)
    {
        var uid = User.Id();
        if (!await db.Products.AnyAsync(p => p.Id == productId)) return NotFound();
        if (await db.Reviews.AnyAsync(r => r.ProductId == productId && r.UserId == uid))
            return Conflict(new { message = "You already reviewed this product" });

        db.Reviews.Add(new Review { ProductId = productId, UserId = uid, Rating = dto.Rating, Comment = dto.Comment.Trim() });
        await db.SaveChangesAsync();
        return await Get(productId);
    }
}
