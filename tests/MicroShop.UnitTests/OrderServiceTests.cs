using System.Diagnostics.Metrics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Api.Data;
using Orders.Api.Services;
using Orders.Api.Telemetry;

namespace MicroShop.UnitTests;

public sealed class OrderServiceTests : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    private readonly OrdersDbContext db = new(
        new DbContextOptionsBuilder<OrdersDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private IMeterFactory MeterFactory => services.GetRequiredService<IMeterFactory>();

    [Fact]
    public async Task Successful_order_is_saved_with_the_correct_total()
    {
        var service = CreateService(new FakeCatalogHandler());

        var result = await service.PlaceOrderAsync(productId: 1, quantity: 3, TestContext.Current.CancellationToken);

        Assert.Null(result.FailureReason);
        Assert.Equal(150m, result.Order!.Total);
        Assert.Equal(1, await db.Orders.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.NoContent, OrderFailure.ProductNotFound)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.NotFound, OrderFailure.ProductNotFound)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.Conflict, OrderFailure.OutOfStock)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.NoContent, OrderFailure.CatalogUnavailable)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable, OrderFailure.CatalogUnavailable)]
    public async Task Failed_order_is_not_saved_and_reports_why(
        HttpStatusCode getProductStatus, HttpStatusCode reserveStatus, string expectedReason)
    {
        var service = CreateService(new FakeCatalogHandler(getProductStatus, reserveStatus));

        var result = await service.PlaceOrderAsync(productId: 1, quantity: 1, TestContext.Current.CancellationToken);

        Assert.Equal(expectedReason, result.FailureReason);
        Assert.Equal(0, await db.Orders.CountAsync(TestContext.Current.CancellationToken));
    }

    // Dashboards and alerts depend on metrics, so treat them as behaviour and test them.
    [Fact]
    public async Task Successful_order_records_the_created_and_value_metrics()
    {
        using var created = new MetricCollector<long>(MeterFactory, OrderMetrics.MeterName, "orders.created");
        using var value = new MetricCollector<double>(MeterFactory, OrderMetrics.MeterName, "orders.value");
        var service = CreateService(new FakeCatalogHandler());

        await service.PlaceOrderAsync(productId: 1, quantity: 2, TestContext.Current.CancellationToken);

        Assert.Equal(1, Assert.Single(created.GetMeasurementSnapshot()).Value);
        Assert.Equal(100, Assert.Single(value.GetMeasurementSnapshot()).Value);
    }

    [Fact]
    public async Task Failed_order_records_the_failed_metric_tagged_with_the_reason()
    {
        using var failed = new MetricCollector<long>(MeterFactory, OrderMetrics.MeterName, "orders.failed");
        var service = CreateService(new FakeCatalogHandler(reserveStatus: HttpStatusCode.Conflict));

        await service.PlaceOrderAsync(productId: 1, quantity: 1, TestContext.Current.CancellationToken);

        var measurement = Assert.Single(failed.GetMeasurementSnapshot());
        Assert.Equal(OrderFailure.OutOfStock, measurement.Tags["reason"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task In_progress_metric_returns_to_zero_even_after_a_failure(HttpStatusCode getProductStatus)
    {
        using var inProgress = new MetricCollector<long>(MeterFactory, OrderMetrics.MeterName, "orders.in_progress");
        var service = CreateService(new FakeCatalogHandler(getProductStatus));

        await service.PlaceOrderAsync(productId: 1, quantity: 1, TestContext.Current.CancellationToken);

        Assert.Equal([1, -1], inProgress.GetMeasurementSnapshot().Select(measurement => measurement.Value));
    }

    public void Dispose()
    {
        db.Dispose();
        services.Dispose();
    }

    private OrderService CreateService(HttpMessageHandler catalogHandler)
    {
        var http = new HttpClient(catalogHandler) { BaseAddress = new Uri("http://catalog") };

        return new OrderService(
            db,
            new CatalogClient(http),
            new OrderMetrics(MeterFactory),
            NullLogger<OrderService>.Instance);
    }
}
