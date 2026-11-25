using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Services;

namespace Orders.Api.Endpoints;

public record CreateOrderRequest(int ProductId, [Range(1, 1000)] int Quantity);

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapGet("/", GetOrders)
            .WithSummary("List all orders");

        orders.MapGet("/{id:guid}", GetOrder)
            .WithSummary("Get one order");

        orders.MapPost("/", CreateOrder)
            .WithSummary("Place an order (products 1-4 work, 98 is slow, 99 fails)")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Ok<List<Order>>> GetOrders(OrdersDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.Orders.AsNoTracking().ToListAsync(cancellationToken));

    private static async Task<Results<Ok<Order>, NotFound>> GetOrder(
        Guid id, OrdersDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([id], cancellationToken);
        return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
    }

    private static async Task<Results<Created<Order>, ProblemHttpResult>> CreateOrder(
        CreateOrderRequest request, OrderService orders, CancellationToken cancellationToken)
    {
        var result = await orders.PlaceOrderAsync(request.ProductId, request.Quantity, cancellationToken);

        if (result.Order is not null)
        {
            return TypedResults.Created($"/orders/{result.Order.Id}", result.Order);
        }

        var statusCode = result.FailureReason switch
        {
            OrderFailure.ProductNotFound => StatusCodes.Status422UnprocessableEntity,
            OrderFailure.OutOfStock => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        return TypedResults.Problem(
            title: "The order could not be placed.",
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["reason"] = result.FailureReason });
    }
}
