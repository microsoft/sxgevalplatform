@description('Alert Action Group Id. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('Storage Account Resource Id. Required.')
param storageAccountResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

// Mapping to configure alert severity by environment
var environmentMapping = {
  ppe: {
    storageAccountAlertDropInAvailability99: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInTransactionCountMedium: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInUsedCapacity: {
      severity: 4
	  enabled: true
    }
  }
  int: {
    storageAccountAlertDropInAvailability99: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInTransactionCountMedium: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInUsedCapacity: {
      severity: 4
	  enabled: true
    }
  }
  prod: {
    storageAccountAlertDropInAvailability99: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInTransactionCountMedium: {
      severity: 4
	  enabled: true
    }
	storageAccountAlertIncreaseInUsedCapacity: {
      severity: 4
	  enabled: true
    }
  }
}

  resource storageAccountAlertDropInAvailability99 'microsoft.insights/metricAlerts@2018-03-01' = {
    name: 'Storage Account Drop in Availability - 99 Percent'
    location: 'global'
    tags: {
      MonitoringCategory: 'Relability $ Availability'
      MonitoringSubCategory: 'Availability'
    }
    properties: {
      description: 'Storage Account Drop in Availability'
	  severity: environmentMapping[environment].storageAccountAlertDropInAvailability99.severity
	  enabled: environmentMapping[environment].storageAccountAlertDropInAvailability99.enabled
      scopes: [
        storageAccountResourceId
      ]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT15M'
      criteria: {
        allOf: [
          {
            threshold: 99
            name: 'Metric1'
            metricNamespace: 'Microsoft.Storage/storageAccounts'
            metricName: 'Availability'
            operator: 'LessThan'
            timeAggregation: 'Average'
            skipMetricValidation: false
            criterionType: 'StaticThresholdCriterion'
          }
        ]
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      }
      autoMitigate: environmentMapping[environment].storageAccountAlertDropInAvailability99.severity == 4
      targetResourceType: 'Microsoft.Storage/storageAccounts'
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

  resource storageAccountAlertIncreaseInTransactionCountMedium 'microsoft.insights/metricAlerts@2018-03-01' = {
    name: 'Storage Account Increase in Transaction Count - Medium Threshold'
    location: 'global'
    tags: {
      MonitoringCategory: 'Infrastructure Dependency'
      MonitoringSubCategory: 'Bottlenecks'
    }
    properties: {
      description: 'Storage Account Increase in Transaction Count'
	  severity: environmentMapping[environment].storageAccountAlertIncreaseInTransactionCountMedium.severity
	  enabled: environmentMapping[environment].storageAccountAlertIncreaseInTransactionCountMedium.enabled
      scopes: [
        storageAccountResourceId
      ]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT15M'
      criteria: {
        allOf: [
          {
            alertSensitivity: 'Medium'
            failingPeriods: {
              numberOfEvaluationPeriods: 4
              minFailingPeriodsToAlert: 4
            }
            name: 'Metric1'
            metricNamespace: 'Microsoft.Storage/storageAccounts'
            metricName: 'Transactions'
            operator: 'GreaterThan'
            timeAggregation: 'Total'
            skipMetricValidation: false
            criterionType: 'DynamicThresholdCriterion'
          }
        ]
        'odata.type': 'Microsoft.Azure.Monitor.MultipleResourceMultipleMetricCriteria'
      }
      autoMitigate: environmentMapping[environment].storageAccountAlertIncreaseInTransactionCountMedium.severity == 4
      targetResourceType: 'Microsoft.Storage/storageAccounts'
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

  resource storageAccountAlertIncreaseInUsedCapacity 'microsoft.insights/metricAlerts@2018-03-01' = {
    name: 'Storage Account Increase in Used Capacity'
    location: 'global'
    tags: {
      MonitoringCategory: 'Infrastructure Dependency'
      MonitoringSubCategory: 'Bottlenecks'
    }
    properties: {
      description: 'Storage Account Increase in Used Capacity'
	  severity: environmentMapping[environment].storageAccountAlertIncreaseInUsedCapacity.severity
	  enabled: environmentMapping[environment].storageAccountAlertIncreaseInUsedCapacity.enabled
      scopes: [
        storageAccountResourceId
      ]
      evaluationFrequency: 'PT5M'
      windowSize: 'PT1H'
      criteria: {
        allOf: [
          {
            threshold: 1073741824
            name: 'Metric1'
            metricNamespace: 'Microsoft.Storage/storageAccounts'
            metricName: 'UsedCapacity'
            operator: 'GreaterThan'
            timeAggregation: 'Average'
            skipMetricValidation: false
            criterionType: 'StaticThresholdCriterion'
          }
        ]
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      }
      autoMitigate: environmentMapping[environment].storageAccountAlertIncreaseInUsedCapacity.severity == 4
      targetResourceType: 'Microsoft.Storage/storageAccounts'
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

