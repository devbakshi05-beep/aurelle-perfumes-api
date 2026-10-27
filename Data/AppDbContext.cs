using Microsoft.EntityFrameworkCore;
using PerfumeShop.Api.Models;

namespace PerfumeShop.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().Property(p => p.Price).HasColumnType("decimal(10,2)");
        b.Entity<Order>().Property(o => o.Total).HasColumnType("decimal(10,2)");
        b.Entity<OrderItem>().Property(o => o.UnitPrice).HasColumnType("decimal(10,2)");

        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<CartItem>().HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
        b.Entity<Review>().HasIndex(r => new { r.ProductId, r.UserId }).IsUnique();
        b.Entity<WishlistItem>().HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();

        // purane orders ke liye default values (migration inhi se purani rows bharta hai)
        b.Entity<Order>().Property(o => o.PaymentMethod).HasDefaultValue("COD");
        b.Entity<Order>().Property(o => o.PaymentStatus).HasDefaultValue("Pending");

        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        b.Entity<Brand>().HasData(
            new Brand { Id = 1, Name = "Aurelle" },
            new Brand { Id = 2, Name = "Noir Essence" },
            new Brand { Id = 3, Name = "Bloom & Co" });

        b.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Midnight Oud", BrandId = 2, Price = 4999, SizeMl = 100, Gender = "Men",
                Description = "Deep, woody and mysterious evening fragrance.",
                TopNotes = "Bergamot, Saffron", HeartNotes = "Oud, Rose", BaseNotes = "Amber, Musk",
                ImageUrl = "https://placehold.co/400x500?text=Midnight+Oud", Stock = 25, IsFeatured = true, CreatedAt = seedDate },
            new Product { Id = 2, Name = "Rose Petal", BrandId = 3, Price = 3499, SizeMl = 50, Gender = "Women",
                Description = "Fresh floral with a soft powdery finish.",
                TopNotes = "Pink Pepper, Lychee", HeartNotes = "Rose, Peony", BaseNotes = "Vanilla, Cedar",
                ImageUrl = "https://placehold.co/400x500?text=Rose+Petal", Stock = 40, IsFeatured = true, CreatedAt = seedDate },
            new Product { Id = 3, Name = "Citrus Aura", BrandId = 1, Price = 2799, SizeMl = 100, Gender = "Unisex",
                Description = "Zesty and energetic, perfect for daytime.",
                TopNotes = "Lemon, Grapefruit", HeartNotes = "Neroli, Mint", BaseNotes = "White Musk",
                ImageUrl = "https://placehold.co/400x500?text=Citrus+Aura", Stock = 60, IsFeatured = false, CreatedAt = seedDate });
    }
}
