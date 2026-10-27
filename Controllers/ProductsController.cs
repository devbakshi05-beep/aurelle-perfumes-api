using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Data;

namespace PerfumeShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProductsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        string? search, int? brandId, string? gender, string? note,
        decimal? minPrice, decimal? maxPrice,
        string sort = "newest", int page = 1, int pageSize = 12)
    {
        var q = _db.Products.Include(p => p.Brand).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Name.Contains(search) || p.Brand!.Name.Contains(search));
        if (brandId.HasValue) q = q.Where(p => p.BrandId == brandId);
        if (!string.IsNullOrWhiteSpace(gender)) q = q.Where(p => p.Gender == gender);
        if (minPrice.HasValue) q = q.Where(p => p.Price >= minPrice);
        if (maxPrice.HasValue) q = q.Where(p => p.Price <= maxPrice);
        if (!string.IsNullOrWhiteSpace(note))
            q = q.Where(p => p.TopNotes.Contains(note) || p.HeartNotes.Contains(note) || p.BaseNotes.Contains(note));

        q = sort switch
        {
            "priceAsc"  => q.OrderBy(p => p.Price),
            "priceDesc" => q.OrderByDescending(p => p.Price),
            "name"      => q.OrderBy(p => p.Name),
            _           => q.OrderByDescending(p => p.CreatedAt)
        };

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Price, p.SizeMl, p.Gender, p.ImageUrl,
                Brand = p.Brand!.Name,
                AvgRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0
            }).ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("featured")]
    public async Task<IActionResult> Featured() =>
        Ok(await _db.Products.Where(p => p.IsFeatured).Take(8)
            .Select(p => new
            {
                p.Id, p.Name, p.Price, p.SizeMl, p.Gender, p.ImageUrl,
                Brand = p.Brand!.Name,
                AvgRating = p.Reviews.Any() ? p.Reviews.Average(r => r.Rating) : 0
            }).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var p = await _db.Products.Include(x => x.Brand)
            .Include(x => x.Reviews).FirstOrDefaultAsync(x => x.Id == id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpGet("brands")]
    public async Task<IActionResult> Brands() => Ok(await _db.Brands.ToListAsync());
}
