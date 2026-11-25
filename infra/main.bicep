// Everything one environment needs in Azure. The same file is deployed to staging
// and production with different parameters, so the two cannot drift apart.

targetScope = 'resourceGroup'

@description('Which environment this is.')
@allowed(['staging', 'production'])
param environmentName string

@description('Catalog image, pinned by digest: ghcr.io/owner/microshop-catalog@sha256:...')
param catalogImage string

@description('Orders image, pinned by digest: ghcr.io/owner/microshop-orders@sha256:...')
param ordersImage string

@description('Creates an alert rule for server errors. Off by default because alert rules can cost a little.')
param deployAlert bool = false

param location string = resourceGroup().location

// ASP.NET Core expects "Staging" or "Production".
var aspNetCoreEnvironment = environmentName == 'production' ? 'Production' : 'Staging'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    environmentName: environmentName
    location: location
    deployAlert: deployAlert
  }
}

module containerEnvironment 'modules/container-environment.bicep' = {
  name: 'container-environment'
  params: {
    environmentName: environmentName
    location: location
    logAnalyticsName: monitoring.outputs.logAnalyticsName
  }
}

module catalog 'modules/container-app.bicep' = {
  name: 'catalog'
  params: {
    serviceName: 'catalog'
    environmentName: environmentName
    location: location
    containerEnvironmentId: containerEnvironment.outputs.id
    image: catalogImage
    aspNetCoreEnvironment: aspNetCoreEnvironment
    appInsightsName: monitoring.outputs.appInsightsName
    isPublic: false // only reachable from other apps in this environment
  }
}

module orders 'modules/container-app.bicep' = {
  name: 'orders'
  params: {
    serviceName: 'orders'
    environmentName: environmentName
    location: location
    containerEnvironmentId: containerEnvironment.outputs.id
    image: ordersImage
    aspNetCoreEnvironment: aspNetCoreEnvironment
    appInsightsName: monitoring.outputs.appInsightsName
    isPublic: true
    extraEnvironmentVariables: [
      {
        // Service discovery: tells Orders what "https+http://catalog" means.
        // Locally, Aspire sets the equivalent variable.
        name: 'services__catalog__https__0'
        value: catalog.outputs.url
      }
    ]
  }
}

output ordersUrl string = orders.outputs.url
