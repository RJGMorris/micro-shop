using System.Diagnostics.Metrics;
using Catalog.Api.Data;
using Catalog.Api.Services;
using Catalog.Api.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace MicroShop.UnitTests;

public sealed class CatalogTests : IDisposable
{
    private const int RareChairId = 4; // starts with 1 in stock

    private readonly ServiceProvider services;

    public CatalogTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        services = new ServiceCollection()
            .AddMetrics()
            .AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton<CatalogMetrics>()
            .BuildServiceProvider();

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    [Theory]
    [InlineData(RareChairId, 1, ReserveStockResult.Reserved, 0)]
    [InlineData(RareChairId, 2, ReserveStockResult.OutOfStock, 1)]
    public async Task Reserving_never_takes_more_than_the_available_stock(
        int productId, int quantity, ReserveStockResult expectedResult, int expectedStock)
    {
        using var db = CreateDbContext();

        var result = await ReserveAsync(db, productId, quantity);

        Assert.Equal(expectedResult, result);
        using var freshDb = CreateDbContext();
        Assert.Equal(expectedStock, freshDb.Products.Single(product => product.Id == productId).Stock);
    }

    [Fact]
    public async Task Reserving_an_unknown_product_reports_not_found()
    {
        using var db = CreateDbContext();

        Assert.Equal(ReserveStockResult.ProductNotFound, await ReserveAsync(db, productId: 12345, quantity: 1));
    }

    [Fact]
    public async Task Two_requests_cannot_both_take_the_last_item()
    {
        // Both requests read the product while one is still in stock.
        using var first = CreateDbContext();
        using var second = CreateDbContext();
        await first.Products.FindAsync([RareChairId], TestContext.Current.CancellationToken);
        await second.Products.FindAsync([RareChairId], TestContext.Current.CancellationToken);

        Assert.Equal(ReserveStockResult.Reserved, await ReserveAsync(first, RareChairId, quantity: 1));
        Assert.Equal(ReserveStockResult.Conflict, await ReserveAsync(second, RareChairId, quantity: 1));
    }

    [Fact]
    public void Stock_gauge_reports_the_total_for_each_category()
    {
        services.GetRequiredService<CatalogMetrics>();
        var meterFactory = services.GetRequiredService<IMeterFactory>();
        using var stock = new MetricCollector<int>(meterFactory, CatalogMetrics.MeterName, "catalog.stock.level");

        stock.RecordObservableInstruments();

        var furniture = stock.GetMeasurementSnapshot().Single(m => Equals(m.Tags["category"], "furniture"));
        Assert.Equal(11, furniture.Value); // 10 desks + 1 rare chair
    }

    public void Dispose() => services.Dispose();

    private CatalogDbContext CreateDbContext() =>
        new(services.GetRequiredService<DbContextOptions<CatalogDbContext>>());

    private static Task<ReserveStockResult> ReserveAsync(CatalogDbContext db, int productId, int quantity) =>
        new StockService(db, NullLogger<StockService>.Instance)
            .ReserveAsync(productId, quantity, TestContext.Current.CancellationToken);
}
