@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('App Service Resource Id. Required.')
param appServiceResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

param instance string

resource appServiceAlert5xxErrorsLow 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - ${location}-${instance} App Service 5xx Errors - Low Threshold'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Reliability'
  }
  properties: {
    description: 'Abnormal Increase in 5xx Errors'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      appServiceResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT1H'
    criteria: {
      allOf: [
        {
          alertSensitivity: 'Low'
          failingPeriods: {
            numberOfEvaluationPeriods: 2
            minFailingPeriodsToAlert: 2
          }
          name: 'Metric1'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'DynamicThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.Web/sites'
    targetResourceRegion: location
    actions: [
      {
        actionGroupId: actionGroupId
        webHookProperties: {
        }
      }
    ]
  }
}
