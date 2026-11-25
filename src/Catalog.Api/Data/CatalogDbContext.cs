using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Keyboard", Category = "electronics", Price = 50m, Stock = 100 },
            new Product { Id = 2, Name = "Monitor", Category = "electronics", Price = 200m, Stock = 25 },
            new Product { Id = 3, Name = "Desk", Category = "furniture", Price = 300m, Stock = 10 },
            new Product { Id = 4, Name = "Rare Chair", Category = "furniture", Price = 900m, Stock = 1 },

            // These two misbehave on purpose. See ChaosSimulator.
            new Product { Id = ChaosSimulator.SlowProductId, Name = "Slow Product", Category = "chaos", Price = 1m, Stock = 1000 },
            new Product { Id = ChaosSimulator.BrokenProductId, Name = "Broken Product", Category = "chaos", Price = 1m, Stock = 1000 });
    }
}
