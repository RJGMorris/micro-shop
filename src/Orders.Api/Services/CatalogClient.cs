using System.Net;

namespace Orders.Api.Services;

/// <summary>The parts of Catalog's product that Orders needs.</summary>
public record CatalogProduct(int Id, string Name, decimal Price);

public enum ReserveOutcome
{
    Reserved,
    ProductNotFound,
    OutOfStock,
}

/// <summary>
/// Calls the Catalog service over HTTP. Retries, timeouts and the circuit breaker
/// come from ServiceDefaults. The "traceparent" header is added to each call
/// automatically, which is what joins both services into a single trace.
/// </summary>
public class CatalogClient(HttpClient http)
{
    /// <summary>Returns null when the product doesn't exist. Throws if Catalog is failing.</summary>
    public async Task<CatalogProduct?> GetProductAsync(int productId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"/products/{productId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken);
    }

    /// <summary>Takes stock for an order. Throws if Catalog is failing.</summary>
    public async Task<ReserveOutcome> ReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(
            $"/products/{productId}/reserve", new { quantity }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return ReserveOutcome.ProductNotFound;
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return ReserveOutcome.OutOfStock;
        }

        response.EnsureSuccessStatusCode();
        return ReserveOutcome.Reserved;
    }
}
