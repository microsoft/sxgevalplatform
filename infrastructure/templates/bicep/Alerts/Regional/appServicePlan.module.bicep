@description('Alert Action Group ID. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('App Service Plan Resource Id. Required.')
param appServicePlanResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

// Mapping to configure alert severity by environment
var environmentMapping = {
  int: {
    appServiceAlertHighCPUUsage80: {
      severity: 4
	    enabled: false
    }
	  appServiceAlertHighMemoryUsage80: {
      severity: 4
	    enabled: false
    }
  }
  ppe: {
    appServiceAlertHighCPUUsage80: {
      severity: 4
	    enabled: false
    }
	  appServiceAlertHighMemoryUsage80: {
      severity: 4
	    enabled: false
    }
  }
  prod: {
    appServiceAlertHighCPUUsage80: {
      severity: 4
	    enabled: true
    }
	  appServiceAlertHighMemoryUsage80: {
      severity: 4
	    enabled: true
    }
  }
}

resource appServiceAlertHighCPUUsage80 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - ${location} App Service High CPU Usage - 80 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Infrastructure Dependency'
    MonitoringSubCategory: 'Bottlenecks'
  }
  properties: {
    description: 'CPU usage too high.'
    severity: environmentMapping[environment].appServiceAlertHighCPUUsage80.severity
    enabled: environmentMapping[environment].appServiceAlertHighCPUUsage80.enabled
    scopes: [
      appServicePlanResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 80
          name: 'Metric1'
          metricNamespace: 'microsoft.web/serverfarms'
          metricName: 'CpuPercentage'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].appServiceAlertHighCPUUsage80.severity == 4
    targetResourceType: 'Microsoft.Web/serverFarms'
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

resource appServiceAlertHighMemoryUsage80 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} - ${location} App Service High Memory Usage - 80 Percent'
  location: 'global'
  tags: {
    MonitoringCategory: 'Infrastructure Dependency'
    MonitoringSubCategory: 'Bottlenecks'
  }
  properties: {
    description: 'Memory usage too high. '
    severity: environmentMapping[environment].appServiceAlertHighMemoryUsage80.severity
    enabled: environmentMapping[environment].appServiceAlertHighMemoryUsage80.enabled
    scopes: [
      appServicePlanResourceId
    ]
    evaluationFrequency: 'PT15M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 80
          name: 'Metric1'
          metricNamespace: 'microsoft.web/serverfarms'
          metricName: 'MemoryPercentage'
          operator: 'GreaterThan'
          timeAggregation: 'Average'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].appServiceAlertHighMemoryUsage80.severity == 4
    targetResourceType: 'Microsoft.Web/serverFarms'
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
