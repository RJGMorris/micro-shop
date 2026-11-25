// One microservice. Reused for Catalog and Orders.

@description('Short name, such as "orders". Also the service name shown in telemetry.')
param serviceName string
param environmentName string
param location string
param containerEnvironmentId string
param image string
param aspNetCoreEnvironment string
param appInsightsName string

@description('true = reachable from the internet, false = only from this environment.')
param isPublic bool

@description('Extra settings for one particular service.')
param extraEnvironmentVariables array = []

var port = 8080

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-${serviceName}-${environmentName}'
  location: location
  properties: {
    environmentId: containerEnvironmentId
    workloadProfileName: 'Consumption'
    configuration: {
      // Single: a new revision only receives traffic once it is ready, and the
      // old one keeps serving until then. A container that never becomes ready
      // therefore does not replace a working one.
      activeRevisionsMode: 'Single'
      ingress: {
        external: isPublic
        targetPort: port
        allowInsecure: false // redirect http to https
      }
      secrets: [
        {
          name: 'appinsights-connection-string'
          value: appInsights.properties.ConnectionString
        }
      ]
      // No registry credentials are needed, because the GHCR images are public.
    }
    template: {
      containers: [
        {
          name: serviceName
          image: image
          resources: {
            cpu: json('0.25') // the smallest size
            memory: '0.5Gi'
          }
          env: concat(
            [
              {
                name: 'ASPNETCORE_ENVIRONMENT'
                value: aspNetCoreEnvironment
              }
              {
                // Azure handles HTTPS and forwards plain HTTP to the container. This
                // makes ASP.NET Core trust the original scheme, so links use https.
                name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
                value: 'true'
              }
              {
                name: 'OTEL_SERVICE_NAME'
                value: serviceName
              }
              {
                name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
                secretRef: 'appinsights-connection-string'
              }
            ],
            extraEnvironmentVariables
          )
          probes: [
            {
              // Is it stuck? Failing restarts the container.
              type: 'Liveness'
              httpGet: {
                path: '/alive'
                port: port
              }
              periodSeconds: 30
            }
            {
              // Can it take traffic? Failing stops traffic to it.
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: port
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0 // COST GUARD: scale to zero when idle. The next request is slow.
        maxReplicas: 1 // In-memory data is not shared, so two copies would disagree.
      }
    }
  }
}

output url string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
