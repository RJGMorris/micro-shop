using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(MicroShop.IntegrationTests.MicroShopFixture))]

namespace MicroShop.IntegrationTests;

/// <summary>
/// Starts the real system once, through the same AppHost you use locally, and
/// shares it between all tests.
/// </summary>
public sealed class MicroShopFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(2);

    private DistributedApplication? app;

    public HttpClient Catalog { get; private set; } = null!;
    public HttpClient Orders { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MicroShop_AppHost>(timeout.Token);

        app = await appHost.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("catalog", timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("orders", timeout.Token);

        Catalog = app.CreateHttpClient("catalog");
        Orders = app.CreateHttpClient("orders");
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }
}
