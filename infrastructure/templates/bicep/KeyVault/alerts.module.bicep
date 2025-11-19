@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Key Vault Resource Id. Required.')
param keyVaultResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

resource keyVaultAlertDropInVaultAvailability99 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} Key Vault - Drop in Availability - 99 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Availability'
  }
  properties: {
    description: 'Key Vault Drop in Availability'
    severity: 4
    enabled: environment == 'prod'
    scopes: [
      keyVaultResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 99
          name: 'Metric1'
          metricNamespace: 'Microsoft.KeyVault/vaults'
          metricName: 'Availability'
          operator: 'LessThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: true
    targetResourceType: 'Microsoft.KeyVault/vaults'
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

resource keyVaultAlertDropInVaultAvailability90 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} Key Vault - Drop in Availability - 90 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Availability'
  }
  properties: {
    description: 'Key Vault Drop in Availability'
    severity: environment == 'prod' ? 3 : 4
    enabled: environment == 'prod'
    scopes: [
      keyVaultResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 90
          name: 'Metric1'
          metricNamespace: 'Microsoft.KeyVault/vaults'
          metricName: 'Availability'
          operator: 'LessThan'
          timeAggregation: 'Average'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
    }
    autoMitigate: environment != 'prod'
    targetResourceType: 'Microsoft.KeyVault/vaults'
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
