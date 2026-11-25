# 5. Interview notes

Short answers, each with code you can talk about. Say what you did **and what you would change at scale**.

## CI/CD

**Walk me through your pipeline.**
A pull request runs formatting, build, unit and integration tests, compiles the Bicep, and builds and scans the images. Merging to main publishes images tagged with the commit. CD deploys those exact images to staging, smoke tests them, waits for approval, then does the same in production. → [`ci.yml`](../.github/workflows/ci.yml), [`cd.yml`](../.github/workflows/cd.yml)

**Why separate CI and CD?**
Different triggers, permissions and failure modes. CI has no Azure access, and I can redeploy an old version without rebuilding.

**What does "build once" protect you from?**
Testing one thing and shipping another. A rebuild can pick up a newer base image or package. I deploy by digest, so staging and production run identical bytes.

**How do you handle secrets?**
Mostly by not having any: OIDC replaces stored cloud passwords. The connection string is read by Bicep and stored as a Container Apps secret. With more secrets, I'd use Key Vault and managed identity. → [`deploy.yml`](../.github/workflows/deploy.yml), [`setup-azure.ps1`](../infra/setup-azure.ps1)

**How do you roll back?**
Redeploy the previous digest. With a database, migrations must be backward compatible (expand and contract).

**How do you secure the pipeline itself?**
Least-privilege tokens, actions pinned to SHAs, images scanned before they are pushed, dependency audit on restore, and deployments only from main.

**A deployment passed but users see errors. What was missing?**
Verification. My smoke test checks the version and a real cross-service call. At scale I'd add a canary with automatic rollback based on error rate. → [`smoke-test.sh`](../scripts/smoke-test.sh)

**How would you measure the pipeline?**
DORA metrics: deployment frequency, lead time for changes, change failure rate, and time to restore service.

## Observability

**What are the three pillars?**
Metrics show *that* something is wrong, traces show *where*, and logs show *why*. The trace id links them.

**A request is slow. How do you investigate?**
Check p95 by service to see when it started and whether it matches a release. Open a slow trace to find the longest span. Read that span's logs. Here, product 98 demonstrates it.

**How does a trace cross services?**
The W3C `traceparent` header carries the trace id on each HTTP call. With a queue, you pass it in the message headers.

**Why OpenTelemetry?**
It's vendor-neutral. The code doesn't know its backend; one method chooses between Aspire and Application Insights. → [`TelemetryExtensions.cs`](../src/MicroShop.ServiceDefaults/TelemetryExtensions.cs)

**Application Insights or Grafana?**
They overlap rather than match. Application Insights stores and analyses telemetry; Grafana mainly visualises it. On Azure I'd start with Application Insights and view it in Grafana. For multi-cloud, I'd consider the Grafana stack (Loki, Tempo, Mimir).

**What is cardinality, and why does it matter?**
The number of distinct tag values. Each one is a stored time series, so a user id as a tag can cause a huge bill. I tag by `reason`, and keep ids in logs and traces. → [`OrderMetrics.cs`](../src/Orders.Api/Telemetry/OrderMetrics.cs)

**Why test metrics?**
Alerts depend on them. If someone renames one, the alert goes silent and nobody notices. → [`OrderServiceTests.cs`](../tests/MicroShop.UnitTests/OrderServiceTests.cs)

**How do you control telemetry cost?**
Sampling, log levels, low cardinality, retention and daily caps. I ignore probe requests in traces and set noisy libraries to Warning.

**Head or tail sampling?**
Head sampling decides at the start: cheap, but it may drop the failures. Tail sampling decides after the trace ends, so it can keep all the errors, but it needs a collector.

**What do you alert on?**
Symptoms that users feel, based on SLOs, and only when someone must act.

## Microservices

**Liveness or readiness?**
Liveness failing restarts the container; readiness failing stops its traffic. Readiness shouldn't check other services, or failures cascade. → [`HealthExtensions.cs`](../src/MicroShop.ServiceDefaults/HealthExtensions.cs)

**How do you handle a failing dependency?**
Timeouts, retries with backoff for safe methods only, a circuit breaker, and a clear error. Retrying a POST needs an idempotency key. → [`ServiceDefaultsExtensions.cs`](../src/MicroShop.ServiceDefaults/ServiceDefaultsExtensions.cs)

**What happens if saving the order fails after reserving stock?**
The stock leaks. I'd use a saga with a compensating "release stock" step, or the outbox pattern with a queue.

**Why Container Apps and not Kubernetes?**
Two small services don't justify running a cluster. Container Apps gives scaling, revisions and probes, and scales to zero. I'd choose AKS for custom networking, operators, or an existing platform team. The images would run there unchanged.

**What would you do differently in real production?**
A real database, authentication, private networking, Key Vault, several replicas, canary releases, independent pipelines for each service, and SLO-based alerts.
