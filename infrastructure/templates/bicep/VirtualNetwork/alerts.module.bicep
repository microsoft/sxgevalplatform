@description('Alert Action Group Name. Required.')
param actionGroupId string

@description('Azure region to deploy the resource. Required.')
param location string

@description('App Service Plan Resource Id. Required.')
param virtualNetworkResourceId string

@description('Environment into which to deploy resources. Required.')
param environment string

param serviceName string

// Mapping to configure alert severity by environment
var environmentMapping = {
  int: {
    isUnderDDoSAttack: {
      severity: 4
	    enabled: true
    }
  }
  ppe: {
    isUnderDDoSAttack: {
      severity: 4
	    enabled: true
    }
  }
  prod: {
    isUnderDDoSAttack: {
      severity: 3
	    enabled: true
    }
  }
}

resource isUnderDDoSAttack 'microsoft.insights/metricAlerts@2018-03-01' = {
  name: '${serviceName} Virtual Network ${location} - Under DDoS Attack'
  location: 'global'
  tags: {
    MonitoringCategory: 'Reliability & Availability'
    MonitoringSubCategory: 'Availability'
  }
  properties: {
    description: 'Virtual Network Under DDoS Attack.'
  severity: environmentMapping[environment].isUnderDDoSAttack.severity
  enabled: environmentMapping[environment].isUnderDDoSAttack.enabled
    scopes: [
      virtualNetworkResourceId
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          threshold: 1
          name: 'Metric1'
          metricNamespace: 'Microsoft.Network/virtualNetworks'
          metricName: 'IfUnderDDoSAttack'
          operator: 'GreaterThanOrEqual'
          timeAggregation: 'Maximum'
          skipMetricValidation: false
          criterionType: 'StaticThresholdCriterion'
        }
      ]
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
    }
    autoMitigate: environmentMapping[environment].isUnderDDoSAttack.severity == 4
    targetResourceType: 'Microsoft.Network/virtualNetworks'
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
