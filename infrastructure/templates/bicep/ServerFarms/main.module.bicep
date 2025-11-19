@description('Unique name for this resource. Required.')
param name string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Alert Action Group ID. Required.')
param actionGroupId string

@description('Name of the Log Analytics Workspace to upload diagnostic logs to. Required.')
param logAnalyticsName string

@description('Environment into which to deploy resources. Required.')
param environment string

@description('If this is a mock app service plan.')
param isMockService bool = false

@description('Azure region abbreviation used to differentiate deployments.')
param regionAbbreviation string

@description('Unique release number for this deployment.')
param releaseNumber string

param serviceName string

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: name
  location: location
  kind: ''
  sku: {
    name: environment == 'prod' ? 'P2v3' : 'P1v3'
    tier: 'PremiumV3'
    size: environment == 'prod' ? 'P2v3' : 'P1v3'
    family: 'Pv3'
    capacity: environment == 'prod' ? 2 : 1
  }
  properties: {
  }
}

resource appInsightsAutoScaleSettings 'Microsoft.Insights/autoscalesettings@2022-10-01' = {
  name: '${name}-autoscale'
  location: location
  tags: {}
  properties: {
    name: '${name}-autoscale'
    enabled: true
    targetResourceUri: appServicePlan.id
    targetResourceLocation: location
    profiles: [
      {
        name: 'DefaultAutoscaleProfile'
        capacity: {
          minimum: environment == 'prod' ? '2' : '1'
          maximum: environment == 'prod' ? '10' : '1'
          default: environment == 'prod' ? '2' : '1'
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              metricResourceLocation: location
              timeGrain: 'PT1M'
              statistic: 'Max'
              timeWindow: 'PT10M'
              timeAggregation: 'Maximum'
              operator: 'GreaterThan'
              threshold: 60
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value:  '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              metricResourceLocation: location
              timeGrain: 'PT1M'
              statistic: 'Max'
              timeWindow: 'PT10M'
              timeAggregation: 'Maximum'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT10M'
            }
          }
          {
            metricTrigger: {
              metricName: 'MemoryPercentage'
              metricResourceUri: appServicePlan.id
              metricResourceLocation: location
              timeGrain: 'PT1M'
              statistic: 'Max'
              timeWindow: 'PT10M'
              timeAggregation: 'Maximum'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value:  '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'MemoryPercentage'
              metricResourceUri: appServicePlan.id
              metricResourceLocation: location
              timeGrain: 'PT1M'
              statistic: 'Max'
              timeWindow: 'PT10M'
              timeAggregation: 'Maximum'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT10M'
            }
          }
        ]
      }
    ]
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = {
  name: logAnalyticsName
}

resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${name}-default-diagnosticSettings'
  scope: appServicePlan
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// App Service Plan Alerts
module appServicePlanAlerts '../Alerts/Regional/appServicePlan.module.bicep' = if (!isMockService) {
  name: 'appServicePlanAlertsDeploy-${regionAbbreviation}-${releaseNumber}'
  dependsOn: []
  params: {
    actionGroupId: actionGroupId
    location: location
    appServicePlanResourceId: appServicePlan.id
    environment: environment
    serviceName: serviceName
  }
}

output resourceId string = appServicePlan.id
