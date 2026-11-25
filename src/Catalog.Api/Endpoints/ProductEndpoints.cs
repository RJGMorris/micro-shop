using System.ComponentModel.DataAnnotations;
using Catalog.Api.Data;
using Catalog.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Endpoints;

public record ReserveStockRequest([Range(1, 1000)] int Quantity);

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var products = app.MapGroup("/products").WithTags("Products");

        products.MapGet("/", GetProducts)
            .WithSummary("List all products");

        products.MapGet("/{id:int}", GetProduct)
            .WithSummary("Get one product (98 is slow and 99 fails, on purpose)");

        products.MapPost("/{id:int}/reserve", ReserveStock)
            .WithSummary("Reserve stock for an order")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Ok<List<Product>>> GetProducts(CatalogDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.Products.AsNoTracking().ToListAsync(cancellationToken));

    private static async Task<Results<Ok<Product>, NotFound>> GetProduct(
        int id, CatalogDbContext db, CancellationToken cancellationToken)
    {
        await ChaosSimulator.ApplyAsync(id, cancellationToken);

        var product = await db.Products.FindAsync([id], cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> ReserveStock(
        int id, ReserveStockRequest request, StockService stock, CancellationToken cancellationToken)
    {
        var result = await stock.ReserveAsync(id, request.Quantity, cancellationToken);

        return result switch
        {
            ReserveStockResult.Reserved => TypedResults.NoContent(),
            ReserveStockResult.ProductNotFound => TypedResults.NotFound(),
            ReserveStockResult.OutOfStock => TypedResults.Problem(
                "Not enough stock.", statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.Problem(
                "The stock changed. Please try again.", statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }
}
