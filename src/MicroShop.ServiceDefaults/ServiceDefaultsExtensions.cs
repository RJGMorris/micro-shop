using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace MicroShop.ServiceDefaults;

/// <summary>
/// Setup that every service shares, so each Program.cs stays short.
/// </summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Adds telemetry, health checks, service discovery and HTTP resilience.</summary>
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.AddTelemetry();
        builder.AddHealthChecksDefaults();

        builder.Services.AddServiceDiscovery();

        // Applies to every HttpClient in the service.
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Adds timeouts, retries and a circuit breaker.
            http.AddStandardResilienceHandler(options =>
            {
                // Only retry safe methods such as GET. Retrying a POST could perform
                // the action twice, for example reserving the same stock two times.
                options.Retry.DisableForUnsafeHttpMethods();
            });

            // Resolves "https+http://catalog" to a real address.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>Maps the operational endpoints: /health, /alive and /version.</summary>
    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        app.MapHealthEndpoints();
        app.MapVersionEndpoint();
        return app;
    }
}
