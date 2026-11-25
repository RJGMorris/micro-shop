using MicroShop.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Orders.Api.Endpoints;
using Orders.Api.Services;
using Orders.Api.Telemetry;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Telemetry, health checks, service discovery and resilience.
builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();

// In-memory database: the data is lost when the process stops.
// Each service owns its own data. Orders never reads Catalog's database.
builder.Services.AddDbContext<OrdersDbContext>(options => options.UseInMemoryDatabase("orders"));
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<OrderMetrics>();

// "catalog" is a service name, not a host. Service discovery resolves it:
// locally from Aspire, in Azure from an environment variable set by Bicep.
// "https+http" means: use https if available, otherwise http.
builder.Services.AddHttpClient<CatalogClient>(client => client.BaseAddress = new Uri("https+http://catalog"));

var app = builder.Build();

// Turns unhandled exceptions into a standard JSON error that includes the trace id.
app.UseExceptionHandler();

// Normally you would restrict API docs in production. They stay on here for learning.
app.MapOpenApi();
// Keep Scalar to plain API docs: no promotional toolbar, AI agent or usage telemetry.
app.MapScalarApiReference(options => options.HideDeveloperTools().DisableAgent().DisableTelemetry());
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.MapServiceDefaults();
app.MapOrderEndpoints();

app.Run();
