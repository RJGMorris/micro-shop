using System.Diagnostics.Metrics;
using Catalog.Api.Data;

namespace Catalog.Api.Telemetry;

/// <summary>Custom metrics for the Catalog service.</summary>
public class CatalogMetrics
{
    public const string MeterName = "MicroShop.Catalog";

    private readonly IServiceScopeFactory scopeFactory;

    public CatalogMetrics(IMeterFactory meterFactory, IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;

        // OBSERVABLE GAUGE: a current value that is read on a schedule.
        // We don't record it; OpenTelemetry calls ReadStockLevels when it exports.
        meterFactory.Create(MeterName).CreateObservableGauge(
            name: "catalog.stock.level",
            observeValues: ReadStockLevels,
            unit: "{item}",
            description: "Items in stock, by category");
    }

    private List<Measurement<int>> ReadStockLevels()
    {
        // This class is a singleton but DbContext is scoped, so it needs its own scope.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Tag by category, not product id. Every distinct tag value creates a new
        // time series, so tags must have few possible values (low cardinality).
        return db.Products
            .GroupBy(product => product.Category)
            .Select(group => new { Category = group.Key, Stock = group.Sum(product => product.Stock) })
            .AsEnumerable()
            .Select(row => new Measurement<int>(row.Stock, new KeyValuePair<string, object?>("category", row.Category)))
            .ToList();
    }
}
