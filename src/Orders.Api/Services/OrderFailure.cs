namespace Orders.Api.Services;

/// <summary>
/// Why an order failed. Used as a metric tag, so this must stay a short, fixed list.
/// </summary>
public static class OrderFailure
{
    public const string ProductNotFound = "product_not_found";
    public const string OutOfStock = "out_of_stock";
    public const string CatalogUnavailable = "catalog_unavailable";
}
