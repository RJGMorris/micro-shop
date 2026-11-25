// Log Analytics stores the telemetry. Application Insights adds the APM features
// on top of it: application map, end-to-end traces and failure analysis.

param environmentName string
param location string
param deployAlert bool

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-microshop-${environmentName}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      // COST GUARD: stop collecting after 0.1 GB in one day. Two environments at
      // this limit for 30 days would total about 6 GB.
      dailyQuotaGb: json('0.1')
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-microshop-${environmentName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Alert on SYMPTOMS that users feel (errors), not on causes such as high CPU.
resource serverErrorAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = if (deployAlert) {
  name: 'alert-server-errors-${environmentName}'
  location: location
  properties: {
    displayName: 'MicroShop ${environmentName}: server errors'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    scopes: [
      logAnalytics.id
    ]
    criteria: {
      allOf: [
        {
          query: 'AppRequests | where toint(ResultCode) >= 500'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
  }
}

output logAnalyticsName string = logAnalytics.name
output appInsightsName string = appInsights.name
