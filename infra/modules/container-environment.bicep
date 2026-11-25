// The Container Apps environment: a shared private network that the apps run in.

param environmentName string
param location string
param logAnalyticsName string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsName
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-microshop-${environmentName}'
  location: location
  properties: {
    // Console output also goes to Log Analytics. If a container crashes before
    // OpenTelemetry starts, this is the only place you will see why.
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    // Consumption plan: pay per second of use, with a free monthly grant.
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

output id string = containerEnvironment.id
