using Catalog.Api.Data;
using Catalog.Api.Endpoints;
using Catalog.Api.Services;
using Catalog.Api.Telemetry;
using MicroShop.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Telemetry, health checks, service discovery and resilience.
builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();

// In-memory database: the data is lost when the process stops.
builder.Services.AddDbContext<CatalogDbContext>(options => options.UseInMemoryDatabase("catalog"));
builder.Services.AddScoped<StockService>();
builder.Services.AddSingleton<CatalogMetrics>();

var app = builder.Build();

// Turns unhandled exceptions into a standard JSON error that includes the trace id.
app.UseExceptionHandler();

// Normally you would restrict API docs in production. They stay on here for learning.
app.MapOpenApi();
// Keep Scalar to plain API docs: no promotional toolbar, AI agent or usage telemetry.
app.MapScalarApiReference(options => options.HideDeveloperTools().DisableAgent().DisableTelemetry());
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapServiceDefaults();
app.MapProductEndpoints();

// Create the database and insert the seed products.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreated();
}

// Nothing injects CatalogMetrics, so create it now to start the stock gauge.
app.Services.GetRequiredService<CatalogMetrics>();

app.Run();
