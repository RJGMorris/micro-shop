using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MicroShop.IntegrationTests;

/// <summary>
/// Tests real HTTP calls between two running services. Unit tests can't catch
/// problems such as a wrong URL, broken JSON or failed service discovery.
/// </summary>
public class OrderFlowTests(MicroShopFixture shop)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Placing_an_order_creates_it_and_reduces_the_stock()
    {
        var stockBefore = await GetStockAsync(productId: 1);

        var response = await PlaceOrderAsync(productId: 1, quantity: 2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(stockBefore - 2, await GetStockAsync(productId: 1));

        var savedOrder = await shop.Orders.GetAsync(response.Headers.Location, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, savedOrder.StatusCode);
    }

    [Theory]
    [InlineData(12345, 1, HttpStatusCode.UnprocessableEntity, "product_not_found")]
    [InlineData(3, 999, HttpStatusCode.Conflict, "out_of_stock")]
    [InlineData(99, 1, HttpStatusCode.ServiceUnavailable, "catalog_unavailable")]
    public async Task Failed_orders_explain_why(
        int productId, int quantity, HttpStatusCode expectedStatus, string expectedReason)
    {
        var response = await PlaceOrderAsync(productId, quantity);

        Assert.Equal(expectedStatus, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        Assert.Equal(expectedReason, problem.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Invalid_input_is_rejected()
    {
        var response = await PlaceOrderAsync(productId: 1, quantity: 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/version")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task Operational_endpoints_respond(string path)
    {
        Assert.Equal(HttpStatusCode.OK, (await shop.Orders.GetAsync(path, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await shop.Catalog.GetAsync(path, CancellationToken)).StatusCode);
    }

    private Task<HttpResponseMessage> PlaceOrderAsync(int productId, int quantity) =>
        shop.Orders.PostAsJsonAsync("/orders", new { productId, quantity }, CancellationToken);

    private async Task<int> GetStockAsync(int productId)
    {
        var product = await shop.Catalog.GetFromJsonAsync<JsonElement>($"/products/{productId}", CancellationToken);
        return product.GetProperty("stock").GetInt32();
    }
}
