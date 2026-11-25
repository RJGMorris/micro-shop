namespace Orders.Api.Data;

public class Order
{
    public Guid Id { get; set; }
    public int ProductId { get; set; }

    // Copied from Catalog, so later price changes don't rewrite old orders.
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
