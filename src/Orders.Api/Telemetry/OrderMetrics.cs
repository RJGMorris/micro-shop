using System.Diagnostics.Metrics;

namespace Orders.Api.Telemetry;

/// <summary>
/// Custom business metrics. Built-in metrics show whether the SYSTEM is healthy;
/// these show whether the BUSINESS is healthy.
/// </summary>
public class OrderMetrics
{
    public const string MeterName = "MicroShop.Orders";

    private readonly Counter<long> created;
    private readonly Counter<long> failed;
    private readonly Histogram<double> value;
    private readonly UpDownCounter<long> inProgress;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        // COUNTER: only goes up. Graph it as a rate, such as orders per minute.
        created = meter.CreateCounter<long>(
            "orders.created", unit: "{order}", description: "Orders placed successfully");

        failed = meter.CreateCounter<long>(
            "orders.failed", unit: "{order}", description: "Orders that failed, by reason");

        // HISTOGRAM: the spread of values, counted into buckets.
        value = meter.CreateHistogram<double>(
            "orders.value",
            unit: null,
            description: "Total value of each order",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [10, 50, 100, 250, 500, 1000, 5000] });

        // UP-DOWN COUNTER: goes up and down, such as work currently in progress.
        inProgress = meter.CreateUpDownCounter<long>(
            "orders.in_progress", unit: "{order}", description: "Orders being processed right now");
    }

    public void OrderStarted() => inProgress.Add(1);

    public void OrderFinished() => inProgress.Add(-1);

    public void OrderCreated(decimal total)
    {
        created.Add(1);
        value.Record((double)total);
    }

    // "reason" has a few fixed values, so it is safe as a tag. An order id would not be.
    public void OrderFailed(string reason) =>
        failed.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
