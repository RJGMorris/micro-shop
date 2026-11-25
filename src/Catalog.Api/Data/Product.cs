using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Data;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }

    // Optimistic concurrency: if two requests change the stock at the same time,
    // the second save fails instead of silently overwriting the first.
    [ConcurrencyCheck]
    public int Stock { get; set; }
}
