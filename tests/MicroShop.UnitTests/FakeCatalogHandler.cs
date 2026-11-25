using System.Net;
using System.Net.Http.Json;

namespace MicroShop.UnitTests;

/// <summary>Stands in for the Catalog service, so unit tests need no network.</summary>
public class FakeCatalogHandler(
    HttpStatusCode getProductStatus = HttpStatusCode.OK,
    HttpStatusCode reserveStatus = HttpStatusCode.NoContent) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post)
        {
            return Task.FromResult(new HttpResponseMessage(reserveStatus));
        }

        var response = new HttpResponseMessage(getProductStatus)
        {
            Content = JsonContent.Create(new { id = 1, name = "Keyboard", price = 50m }),
        };
        return Task.FromResult(response);
    }
}
