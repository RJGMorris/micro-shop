// The AppHost describes the whole system for LOCAL DEVELOPMENT.
// Running it starts both services, connects them, and opens the Aspire dashboard
// (logs, traces and metrics). It is not deployed: in Azure, the Bicep files in
// /infra do this job instead.

var builder = DistributedApplication.CreateBuilder(args);

var catalog = builder.AddDotnetProject("catalog", "../Catalog.Api/Catalog.Api.csproj")
    .WithHttpHealthCheck("/health");

builder.AddDotnetProject("orders", "../Orders.Api/Orders.Api.csproj")
    .WithHttpHealthCheck("/health")
    // Service discovery: lets Orders call "https+http://catalog" without knowing the port.
    .WithReference(catalog)
    // Don't start Orders until Catalog reports healthy.
    .WaitFor(catalog);

builder.Build().Run();
