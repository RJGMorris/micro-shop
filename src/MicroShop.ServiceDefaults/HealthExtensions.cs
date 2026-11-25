using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroShop.ServiceDefaults;

/// <summary>
/// Health endpoints that the container platform polls.
/// <list type="bullet">
/// <item><b>/alive</b> (liveness): is the process stuck? A failure RESTARTS the container.</item>
/// <item><b>/health</b> (readiness): can it take traffic? A failure STOPS TRAFFIC to it.</item>
/// </list>
/// Orders deliberately does not check Catalog here. If it did, one Catalog outage
/// would also take Orders offline (a cascading failure).
/// </summary>
public static class HealthExtensions
{
    public const string ReadinessPath = "/health";
    public const string LivenessPath = "/alive";

    private const string LiveTag = "live";

    public static WebApplicationBuilder AddHealthChecksDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            // A service with a real database would add an untagged check for it here,
            // so that it affects readiness but not liveness.
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        return builder;
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Readiness runs every check.
        app.MapHealthChecks(ReadinessPath);

        // Liveness runs only the checks tagged "live".
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
        });
    }
}
