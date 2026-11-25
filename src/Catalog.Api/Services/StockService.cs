using Catalog.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Services;

public enum ReserveStockResult
{
    Reserved,
    ProductNotFound,
    OutOfStock,
    Conflict,
}

/// <summary>Business rules for stock.</summary>
public class StockService(CatalogDbContext db, ILogger<StockService> logger)
{
    public async Task<ReserveStockResult> ReserveAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([productId], cancellationToken);

        if (product is null)
        {
            return ReserveStockResult.ProductNotFound;
        }

        if (product.Stock < quantity)
        {
            // Structured logging: {ProductId} and the others become searchable fields.
            // Don't use $"..." here, because that flattens them into plain text.
            logger.LogWarning(
                "Not enough stock for product {ProductId}: requested {Quantity}, available {Stock}",
                productId, quantity, product.Stock);
            return ReserveStockResult.OutOfStock;
        }

        product.Stock -= quantity;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Stock for product {ProductId} was changed by another request", productId);
            return ReserveStockResult.Conflict;
        }

        logger.LogInformation(
            "Reserved {Quantity} of product {ProductId}, {Stock} left",
            quantity, productId, product.Stock);
        return ReserveStockResult.Reserved;
    }
}
