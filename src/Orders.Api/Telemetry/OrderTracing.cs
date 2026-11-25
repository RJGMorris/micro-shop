using System.Diagnostics;

namespace Orders.Api.Telemetry;

/// <summary>
/// Creates custom spans. .NET calls a span an "Activity" and a tracer an "ActivitySource".
/// </summary>
public static class OrderTracing
{
    public static readonly ActivitySource Source = new("MicroShop.Orders");
}
