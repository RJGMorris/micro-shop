using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MicroShop.ServiceDefaults;

/// <summary>
/// Configures the three pillars of observability: logs, metrics and traces.
/// The services only use vendor-neutral OpenTelemetry APIs. The backend that
/// receives the data is chosen in one place: <see cref="AddExporter"/>.
/// </summary>
public static class TelemetryExtensions
{
    // Every custom meter and activity source starts with this prefix,
    // so new ones are collected automatically.
    private const string CustomTelemetryNames = "MicroShop.*";

    public static WebApplicationBuilder AddTelemetry(this WebApplicationBuilder builder)
    {
        // Pillar 1: LOGS - what happened. Sent with the trace id attached.
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            // Labels added to all telemetry, so you can compare versions and environments.
            .ConfigureResource(resource => resource.AddAttributes(new Dictionary<string, object>
            {
                ["service.version"] = VersionEndpoint.GetVersion(builder.Configuration),
                ["deployment.environment.name"] = builder.Environment.EnvironmentName,
            }))

            // Pillar 2: METRICS - how much and how fast. Cheap numbers for dashboards and alerts.
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation() // incoming requests
                .AddHttpClientInstrumentation() // outgoing requests
                .AddRuntimeInstrumentation()    // garbage collector, threads, memory
                .AddMeter(CustomTelemetryNames))

            // Pillar 3: TRACES - where the time went, across services.
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    // Probes run every few seconds and would drown out real traffic.
                    options.Filter = context => !IsOperationalEndpoint(context.Request.Path))
                .AddHttpClientInstrumentation()
                .AddSource(CustomTelemetryNames));

        AddExporter(telemetry, builder);

        return builder;
    }

    /// <summary>Chooses where telemetry is sent, based on configuration.</summary>
    private static void AddExporter(OpenTelemetryBuilder telemetry, WebApplicationBuilder builder)
    {
        // Set by Bicep when running in Azure.
        var azureMonitor = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        // Set by Aspire locally. Would also be used for Grafana Cloud or any OTLP backend.
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        if (!string.IsNullOrWhiteSpace(azureMonitor))
        {
            telemetry.UseAzureMonitorExporter(options =>
            {
                options.ConnectionString = azureMonitor;

                // Keep every trace: traffic is tiny and we want to see everything.
                // With real traffic, lower this to control cost (0.1 = keep 10%).
                options.TracesPerSecond = null;
                options.SamplingRatio = 1.0f;
            });
        }
        else if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            telemetry.UseOtlpExporter();
        }
    }

    private static bool IsOperationalEndpoint(PathString path) =>
        path.StartsWithSegments(HealthExtensions.ReadinessPath)
        || path.StartsWithSegments(HealthExtensions.LivenessPath)
        || path.StartsWithSegments(VersionEndpoint.Path);
}
