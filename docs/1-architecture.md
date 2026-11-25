# 1. Architecture

## The projects

| Project | Job | Deployed? |
|---|---|---|
| `Catalog.Api` | Owns products and stock | Yes |
| `Orders.Api` | Owns orders; calls Catalog | Yes |
| `MicroShop.ServiceDefaults` | Shared telemetry, health checks and resilience | Inside both services |
| `MicroShop.AppHost` | Runs everything locally (Aspire) | No |
| `MicroShop.UnitTests` | Fast tests with no network | No |
| `MicroShop.IntegrationTests` | Start the real system and call it | No |

## What Aspire does

**Locally**, the AppHost starts both services, tells Orders where Catalog is, and provides a dashboard.

**In production** it isn't needed. Each service is an ordinary Docker image. The AppHost's jobs are done by:

| Job | Locally | In Azure |
|---|---|---|
| Start the services | AppHost | Container Apps |
| Service discovery | AppHost sets `services__catalog__...` | Bicep sets `services__catalog__...` |
| Telemetry | Aspire dashboard | Application Insights |
| Restart unhealthy services | (you) | Liveness and readiness probes |

The application code is identical in both. Only configuration changes.

### Other ways to deploy

Aspire can also deploy for you. `aspire deploy` goes straight to a target such as Container Apps, and `aspire publish` generates deployment files. This project uses hand-written Bicep so that each step is visible.

## Decisions worth explaining

**Each service owns its data.** Orders never reads Catalog's database. It copies the product name and price into the order, so later price changes don't alter history.

**Retries only for safe methods.** A failed GET is retried. A POST is not, because the first attempt might have worked and reserving stock twice would be wrong. The alternative is an idempotency key.

**Liveness and readiness are different.** Failing liveness restarts the container. Failing readiness only stops its traffic. Orders' readiness doesn't check Catalog, because then one Catalog outage would take down both.

**Optimistic concurrency on stock.** If two requests take the last item, the second save fails instead of overselling.

**Failure is expected.** When Catalog is down, Orders returns a clear 503 with a reason rather than crashing.

## Known limitations

These are deliberate, to keep the project small:

- **Data is lost on restart.** It is in memory, so each service is limited to one replica.
- **Stock can leak.** If saving an order fails after reserving stock, the stock stays reserved. Real systems use a saga or the outbox pattern.
- **No authentication.** Anyone with the URL can place an order.
- **Scalar is public.** Normally you would restrict API docs in production.
- **Calls are synchronous.** A message queue would let Orders accept work while Catalog is down.
- **One repository and one pipeline.** Both services are released together. Larger teams usually release services independently.
