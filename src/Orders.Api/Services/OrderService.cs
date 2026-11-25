using System.Diagnostics;
using Orders.Api.Data;
using Orders.Api.Telemetry;
using Polly;

namespace Orders.Api.Services;

/// <summary>Holds either the created order or the reason it failed.</summary>
public record PlaceOrderResult(Order? Order, string? FailureReason);

/// <summary>
/// Places orders. This is the main place to see logs, metrics and traces used together.
/// </summary>
public class OrderService(
    OrdersDbContext db,
    CatalogClient catalog,
    OrderMetrics metrics,
    ILogger<OrderService> logger)
{
    public async Task<PlaceOrderResult> PlaceOrderAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        // Custom span: groups the steps below into one named block in the trace.
        // Ids are fine as span tags and log fields, but NOT as metric tags.
        using var activity = OrderTracing.Source.StartActivity("PlaceOrder");
        activity?.SetTag("order.product_id", productId);
        activity?.SetTag("order.quantity", quantity);

        metrics.OrderStarted();
        try
        {
            var product = await catalog.GetProductAsync(productId, cancellationToken);
            if (product is null)
            {
                return Fail(activity, OrderFailure.ProductNotFound, productId);
            }

            var outcome = await catalog.ReserveStockAsync(productId, quantity, cancellationToken);
            if (outcome == ReserveOutcome.ProductNotFound)
            {
                return Fail(activity, OrderFailure.ProductNotFound, productId);
            }

            if (outcome == ReserveOutcome.OutOfStock)
            {
                return Fail(activity, OrderFailure.OutOfStock, productId);
            }

            // Known limitation: if this save fails, the stock stays reserved.
            // Real systems fix that with a saga or the outbox pattern.
            var order = await SaveOrderAsync(product, quantity, cancellationToken);

            activity?.SetTag("order.id", order.Id);
            metrics.OrderCreated(order.Total);
            logger.LogInformation(
                "Order {OrderId} created for product {ProductId}, total {OrderTotal}",
                order.Id, productId, order.Total);

            return new PlaceOrderResult(order, FailureReason: null);
        }
        // HttpRequestException: Catalog failed, even after retries.
        // ExecutionRejectedException: a Polly timeout or an open circuit breaker.
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException)
        {
            // A dependency failure is a real error, so mark the span as failed.
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(exception, "Catalog was unavailable while ordering product {ProductId}", productId);

            return Fail(activity, OrderFailure.CatalogUnavailable, productId);
        }
        finally
        {
            metrics.OrderFinished();
        }
    }

    private async Task<Order> SaveOrderAsync(CatalogProduct product, int quantity, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.CreateVersion7(),
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity,
            Total = product.Price * quantity,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    private PlaceOrderResult Fail(Activity? activity, string reason, int productId)
    {
        activity?.SetTag("order.failure_reason", reason);
        metrics.OrderFailed(reason);
        logger.LogWarning("Order for product {ProductId} failed: {FailureReason}", productId, reason);

        return new PlaceOrderResult(Order: null, reason);
    }
}
